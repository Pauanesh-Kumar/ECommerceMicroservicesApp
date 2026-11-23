using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ProductService.Contracts;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence;

public sealed class TransactionalInventoryProcessor(IMongoClient client, IMongoDatabase db)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    private readonly IMongoCollection<Product> _products = db.GetCollection<Product>("products");
    private readonly IMongoCollection<InboxRecord> _legacyInbox = db.GetCollection<InboxRecord>("inbox");
    private readonly IMongoCollection<BsonDocument> _inbox = db.GetCollection<BsonDocument>("inventory_inbox");
    private readonly IMongoCollection<BsonDocument> _reservations = db.GetCollection<BsonDocument>("inventory_reservations");
    private readonly IMongoCollection<BsonDocument> _outbox = db.GetCollection<BsonDocument>("inventory_outbox");

    public async Task ProcessAsync(string topic, string raw, Guid eventId, bool reserve, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(topic + "\n" + raw)));
        var created = reserve ? JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(raw, Json) : null;
        var released = reserve ? null : JsonSerializer.Deserialize<StockReleaseRequestedEvent>(raw, Json);
        var orderId = created?.OrderId ?? released?.OrderId ?? Guid.Empty;
        var items = created?.Items ?? released?.Items;
        if (orderId == Guid.Empty || items is null || items.Count == 0 || items.Any(i => i.ProductId == Guid.Empty || i.Quantity <= 0))
            throw new InvalidDataException("Invalid inventory event.");
        var lines = items.GroupBy(i => i.ProductId).Select(g => new OrderCreatedLineItem(g.Key, checked(g.Sum(i => i.Quantity))))
            .OrderBy(i => i.ProductId).ToArray();
        var canonical = JsonSerializer.Serialize(lines, Json);
        var eventKey = eventId.ToString();
        var orderKey = orderId.ToString();
        using var session = await client.StartSessionAsync(cancellationToken: ct);
        await session.WithTransactionAsync(async (s, token) =>
        {
            var prior = await _inbox.Find(s, Builders<BsonDocument>.Filter.Eq("_id", eventKey)).FirstOrDefaultAsync(token);
            if (prior is not null)
            {
                if (prior["hash"].AsString != hash) throw new InvalidDataException("Inventory event ID was reused with a different payload.");
                return true;
            }
            if (await _legacyInbox.Find(s, x => x.EventId == eventId).AnyAsync(token))
                throw new InvalidOperationException("Legacy inventory inbox event requires operator reconciliation before replay.");

            var existing = await _reservations.Find(s, Builders<BsonDocument>.Filter.Eq("_id", orderKey)).FirstOrDefaultAsync(token);
            if (existing is not null && existing["items"].AsString != canonical)
                throw new InvalidDataException("Inventory event conflicts with the original order items.");
            if (reserve && existing is null)
            {
                var unavailable = new List<Guid>();
                foreach (var item in lines)
                {
                    var product = await _products.Find(s, p => p.Id == item.ProductId).FirstOrDefaultAsync(token);
                    if (product is null || product.Stock < item.Quantity) unavailable.Add(item.ProductId);
                }
                if (unavailable.Count == 0)
                {
                    foreach (var item in lines)
                    {
                        var filter = Builders<Product>.Filter.Eq(p => p.Id, item.ProductId) & Builders<Product>.Filter.Gte(p => p.Stock, item.Quantity);
                        var update = Builders<Product>.Update.Inc(p => p.Stock, -item.Quantity).Inc(p => p.ReservedStock, item.Quantity).Set(p => p.UpdatedAtUtc, DateTime.UtcNow);
                        var result = await _products.UpdateOneAsync(s, filter, update, cancellationToken: token);
                        if (result.ModifiedCount != 1) throw new InvalidOperationException("Stock changed during reservation; retry the event.");
                    }
                }
                var succeeded = unavailable.Count == 0;
                await _reservations.InsertOneAsync(s, new BsonDocument { { "_id", orderKey }, { "items", canonical }, { "state", succeeded ? "Reserved" : "Rejected" } }, cancellationToken: token);
                object payload = succeeded
                    ? new StockReservedIntegrationEvent(orderId, lines.Select(x => x.ProductId).ToArray(), DateTime.UtcNow)
                    : new StockReservationFailedIntegrationEvent(orderId, "Insufficient stock", unavailable, DateTime.UtcNow);
                var type = succeeded ? "StockReserved" : "StockReservationFailed";
                await _outbox.InsertOneAsync(s, new BsonDocument { { "_id", Guid.NewGuid().ToString() }, { "key", orderKey }, { "payload", JsonSerializer.Serialize(new EventEnvelope(type, JsonSerializer.Serialize(payload, Json)), Json) }, { "createdAt", DateTime.UtcNow }, { "published", false } }, cancellationToken: token);
            }
            else if (!reserve)
            {
                if (existing is null) throw new InvalidOperationException("No recorded inventory reservation exists for release; reconcile legacy or out-of-order events.");
                if (existing["state"].AsString == "Reserved")
                {
                    foreach (var item in lines)
                    {
                        var filter = Builders<Product>.Filter.Eq(p => p.Id, item.ProductId) & Builders<Product>.Filter.Gte(p => p.ReservedStock, item.Quantity);
                        var update = Builders<Product>.Update.Inc(p => p.Stock, item.Quantity).Inc(p => p.ReservedStock, -item.Quantity).Set(p => p.UpdatedAtUtc, DateTime.UtcNow);
                        var result = await _products.UpdateOneAsync(s, filter, update, cancellationToken: token);
                        if (result.ModifiedCount != 1) throw new InvalidOperationException("Reserved inventory does not match the release.");
                    }
                    await _reservations.UpdateOneAsync(s, Builders<BsonDocument>.Filter.Eq("_id", orderKey), Builders<BsonDocument>.Update.Set("state", "Released"), cancellationToken: token);
                }
                else if (existing["state"].AsString != "Released")
                    throw new InvalidOperationException("Cannot release a rejected inventory reservation.");
            }
            await _inbox.InsertOneAsync(s, new BsonDocument { { "_id", eventKey }, { "hash", hash }, { "processedAt", DateTime.UtcNow } }, cancellationToken: token);
            return true;
        }, new TransactionOptions(readConcern: ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority), ct);
    }
}

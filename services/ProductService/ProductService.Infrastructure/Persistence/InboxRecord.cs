using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace ProductService.Infrastructure.Persistence;

/// <summary>
/// Inbox pattern for Kafka consumer idempotency. Every consumed message's EventId
/// (Kafka header, same convention as UserEventPublisher) is inserted here BEFORE
/// processing. MongoDB's unique _id index rejects redelivery of the same EventId.
/// </summary>
public sealed class InboxRecord
{
    [BsonId] public Guid EventId { get; init; }
    public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
}

public sealed class InboxStore(IMongoDatabase database)
{
    private readonly IMongoCollection<InboxRecord> _collection = database.GetCollection<InboxRecord>("inbox");

    /// <returns>true if this is the first time we've seen eventId (caller should process it).</returns>
    public async Task<bool> TryMarkProcessedAsync(Guid eventId, CancellationToken ct = default)
    {
        try
        {
            await _collection.InsertOneAsync(new InboxRecord { EventId = eventId }, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }
}

using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ProductService.Infrastructure.Kafka;

public sealed class InventoryOutboxHostedService(
    IMongoDatabase db, IProducer<string, string> producer, IOptions<ProductKafkaOptions> options,
    ILogger<InventoryOutboxHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outbox = db.GetCollection<BsonDocument>("inventory_outbox");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await outbox.Find(Builders<BsonDocument>.Filter.Eq("published", false))
                    .Sort(Builders<BsonDocument>.Sort.Ascending("createdAt")).Limit(100).ToListAsync(stoppingToken);
                foreach (var item in pending)
                {
                    var id = Guid.Parse(item["_id"].AsString);
                    await producer.ProduceAsync(options.Value.ProductStockTopic, new Message<string, string>
                    {
                        Key = item["key"].AsString,
                        Value = item["payload"].AsString,
                        Headers = new Headers { { "eventId", id.ToByteArray() } }
                    }, stoppingToken);
                    await outbox.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", item["_id"]),
                        Builders<BsonDocument>.Update.Set("published", true).Set("publishedAt", DateTime.UtcNow), cancellationToken: stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Inventory outbox publish failed; pending events will be retried"); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}

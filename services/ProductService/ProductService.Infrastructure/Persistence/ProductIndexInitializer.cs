using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Persistence;

/// <summary>
/// Runs once at startup — ensures the compound index that keyset pagination depends on
/// (CreatedAtUtc, _id), plus the Sku uniqueness constraint and Category filter index.
/// This is the piece that was flagged as a known gap last round (an index/store defined
/// but never wired at startup); it is now registered via AddHostedService in
/// InfrastructureServiceExtensions so it always runs.
/// </summary>
public sealed class ProductIndexInitializer(
    IMongoDatabase database, ILogger<ProductIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var hello = await database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        if (!hello.Contains("setName") && (!hello.Contains("msg") || hello["msg"] != "isdbgrid"))
            throw new InvalidOperationException("Product inventory transactions require a MongoDB replica set or sharded cluster.");

        var products = database.GetCollection<Product>("products");
        await products.Indexes.CreateManyAsync([
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(p => p.CreatedAtUtc).Ascending(p => p.Id)),
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(p => p.Sku), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.Category))
        ], ct);
        foreach (var (name, field) in new[] { ("inventory_inbox", "processedAt"), ("inventory_reservations", "state"), ("inventory_outbox", "published") })
            await database.GetCollection<BsonDocument>(name).Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(field)), cancellationToken: ct);

        logger.LogInformation("[ProductService] Product and transactional inventory indexes ensured.");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

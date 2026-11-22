using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    IMongoDatabase database, InboxStore inboxStore, ILogger<ProductIndexInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var products = database.GetCollection<Product>("products");

        await products.Indexes.CreateManyAsync([
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(p => p.CreatedAtUtc).Ascending(p => p.Id)), // keyset pagination
            new CreateIndexModel<Product>(
                Builders<Product>.IndexKeys.Ascending(p => p.Sku), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Product>(Builders<Product>.IndexKeys.Ascending(p => p.Category))
        ], ct);

        await inboxStore.EnsureIndexesAsync(ct);
        logger.LogInformation("[ProductService] Indexes ensured (products cursor+sku+category, inbox eventId).");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

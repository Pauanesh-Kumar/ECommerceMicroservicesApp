using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace NotificationService.Infrastructure.Persistence.MongoDb;

/// <summary>
/// Runs once at startup to ensure all required MongoDB indexes exist.
/// Idempotent — safe to run on every startup.
/// </summary>
internal sealed class MongoDbInitializer : IHostedService
{
    private readonly IMongoCollection<NotificationDocument> _collection;
    private readonly ILogger<MongoDbInitializer> _logger;

    public MongoDbInitializer(
        IMongoDatabase database,
        IOptions<MongoDbSettings> options,
        ILogger<MongoDbInitializer> logger)
    {
        _collection = database.GetCollection<NotificationDocument>(options.Value.CollectionName);
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[MongoDB] Creating indexes on 'notifications' collection...");

        var indexModels = new List<CreateIndexModel<NotificationDocument>>
        {
            // Fast lookup by user
            new(Builders<NotificationDocument>.IndexKeys.Ascending(d => d.UserId)),

            // Filter by status
            new(Builders<NotificationDocument>.IndexKeys.Ascending(d => d.Status)),

            // Filter by event type
            new(Builders<NotificationDocument>.IndexKeys.Ascending(d => d.EventType)),

            // Default sort: newest first
            new(Builders<NotificationDocument>.IndexKeys.Descending(d => d.CreatedAtUtc)),

            // Compound: user + time (most common query pattern)
            new(Builders<NotificationDocument>.IndexKeys
                .Ascending(d => d.UserId)
                .Descending(d => d.CreatedAtUtc))
        };

        await _collection.Indexes.CreateManyAsync(indexModels, cancellationToken);
        _logger.LogInformation("[MongoDB] Indexes ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
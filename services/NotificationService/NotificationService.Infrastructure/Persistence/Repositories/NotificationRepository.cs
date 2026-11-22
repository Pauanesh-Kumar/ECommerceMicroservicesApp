using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Persistence.MongoDb;

namespace NotificationService.Infrastructure.Persistence.Repositories;

/// <summary>
/// MongoDB implementation of INotificationRepository.
/// Follows the Unit-of-Work pattern used by the existing command handlers:
///   AddAsync()        → stages the notification in memory (not yet persisted)
///   SaveChangesAsync()→ upserts the staged document to MongoDB
/// This preserves the handler contract (add → mutate status → save).
/// </summary>
internal sealed class NotificationRepository : INotificationRepository
{
    private readonly IMongoCollection<NotificationDocument> _collection;
    private readonly List<Notification?> _staged = new(); // unit-of-work staging area

    public NotificationRepository(
        IMongoDatabase database,
        IOptions<MongoDbSettings> options)
    {
        _collection = database.GetCollection<NotificationDocument>(options.Value.CollectionName);
    }

    // ── Read operations ───────────────────────────────────────────────────────

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await _collection
            .Find(d => d.Id == id)
            .FirstOrDefaultAsync(ct);

        return doc?.ToDomain();
    }

    public async Task<IReadOnlyList<Notification>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        var docs = await _collection
            .Find(d => d.UserId == userId)
            .SortByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

        return docs.ConvertAll(d => d.ToDomain());
    }

    public async Task<IReadOnlyList<Notification>> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var docs = await _collection
            .Find(FilterDefinition<NotificationDocument>.Empty)
            .SortByDescending(d => d.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);

        return docs.ConvertAll(d => d.ToDomain());
    }

    // ── Write operations (unit-of-work) ───────────────────────────────────────

    /// <summary>Stages the notification — does NOT write to MongoDB yet.</summary>
    public Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        _staged.Add(notification);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Persists the staged notification (upsert).
    /// Called by the handler after status is mutated (MarkSent / MarkFailed).
    /// </summary>
    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        if (_staged.Count == 0) return;

        var options = new ReplaceOptions { IsUpsert = true };

        var writes = _staged.Select(n =>
        {
            var filter = Builders<NotificationDocument>.Filter.Eq(d => d.Id, n.Id);
            return new ReplaceOneModel<NotificationDocument>(filter, n.ToDocument())
            {
                IsUpsert = true
            };
        }).ToList();

        if (writes.Count == 0) return;

        await _collection.BulkWriteAsync(writes, cancellationToken: ct);
       
        _staged.Clear();
    }
}
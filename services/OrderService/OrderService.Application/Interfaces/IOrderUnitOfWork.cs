namespace OrderService.Application.Interfaces;

/// <summary>
/// Abstraction over the transactional-outbox + idempotency persistence concerns, so
/// Application command handlers never take a direct dependency on EF Core / DbContext
/// (that would leak an Infrastructure concern up into Application). Infrastructure
/// provides the concrete EF Core-backed implementation (EfOrderUnitOfWork) — same DIP
/// boundary UserService already applies via IUnitOfWork, generalized here to also cover
/// outbox enqueue + idempotency lookups since those always need to commit atomically
/// with the aggregate change in the same SaveChangesAsync call.
/// </summary>
public interface IOrderUnitOfWork
{
    Task<Guid?> FindOrderIdByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    void RecordIdempotencyKey(string idempotencyKey, Guid orderId);
    void EnqueueOutboxMessage(string topic, string key, string payload);
    Task SaveChangesAsync(CancellationToken ct = default);
}

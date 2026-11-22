using Microsoft.EntityFrameworkCore;
using OrderService.Application.Interfaces;

namespace OrderService.Infrastructure.Persistence;

/// <summary>EF Core-backed implementation of IOrderUnitOfWork — the only place in this
/// service where Outbox/Inbox/Idempotency tables are touched directly. Application
/// command handlers depend only on the interface (see Application/Interfaces/IOrderUnitOfWork.cs).</summary>
public sealed class EfOrderUnitOfWork(OrderDbContext db) : IOrderUnitOfWork
{
    public async Task<Guid?> FindOrderIdByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var record = await db.IdempotencyRecords.FirstOrDefaultAsync(r => r.IdempotencyKey == idempotencyKey, ct);
        return record?.OrderId;
    }

    public void RecordIdempotencyKey(string idempotencyKey, Guid orderId)
        => db.IdempotencyRecords.Add(new IdempotencyRecord { IdempotencyKey = idempotencyKey, OrderId = orderId });

    public void EnqueueOutboxMessage(string topic, string key, string payload)
        => db.OutboxMessages.Add(new OutboxMessage { Topic = topic, Key = key, Payload = payload });

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}

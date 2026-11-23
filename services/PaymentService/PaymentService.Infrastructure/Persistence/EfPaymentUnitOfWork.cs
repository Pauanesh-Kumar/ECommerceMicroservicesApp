using Microsoft.EntityFrameworkCore;
using Npgsql;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence;

/// <summary>EF Core-backed implementation of IPaymentUnitOfWork. TrySaveChangesAsync
/// swallows unique-constraint violations (OrderId uniqueness = the idempotency guard)
/// and reports them as "false" rather than throwing — the Application layer treats
/// losing that race as a normal no-op, not an error.</summary>
public sealed class EfPaymentUnitOfWork(PaymentDbContext db) : IPaymentUnitOfWork
{
    public Task<Payment?> FindByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => db.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public Task<Payment?> FindByProviderOrderIdAsync(string providerOrderId, CancellationToken ct = default)
        => db.Payments.FirstOrDefaultAsync(p => p.ProviderOrderId == providerOrderId, ct);

    public void Add(Payment payment) => db.Payments.Add(payment);

    public void EnqueueOutboxMessage(string topic, string key, string payload)
        => db.OutboxMessages.Add(new OutboxMessage { Topic = topic, Key = key, Payload = payload });

    public void RecordInboxEvent(string eventId) => db.InboxMessages.Add(new InboxMessage { EventId = eventId });

    public async Task<bool> TrySaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false; // unique-constraint race lost (OrderId or InboxMessage.EventId) — treat as no-op
        }
    }
}

namespace PaymentService.Application.Interfaces;

/// <summary>Abstraction over the Payment aggregate + transactional-outbox persistence,
/// so Application handlers never reference EF Core / PaymentDbContext directly — same
/// DIP boundary as OrderService.Application.Interfaces.IOrderUnitOfWork.</summary>
public interface IPaymentUnitOfWork
{
    Task<Domain.Entities.Payment?> FindByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Domain.Entities.Payment?> FindByProviderOrderIdAsync(string providerOrderId, CancellationToken ct = default);
    void Add(Domain.Entities.Payment payment);
    void EnqueueOutboxMessage(string topic, string key, string payload);
    void RecordInboxEvent(string eventId);
    Task<bool> TrySaveChangesAsync(CancellationToken ct = default); // false = unique-constraint race lost, treat as no-op
}

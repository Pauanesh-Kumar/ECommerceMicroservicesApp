using PaymentService.Domain.Entities;

namespace PaymentService.Domain.Repositories;

public sealed record CursorPageResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);

public interface IPaymentRepository
{
    Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Payment?> GetByProviderOrderIdAsync(string providerOrderId, CancellationToken ct = default);

    /// <summary>Keyset/cursor pagination on (CreatedAtUtc, Id) — admin ledger view.</summary>
    Task<CursorPageResult<Payment>> GetByCursorAsync(string? cursor, int limit, Guid? userId, CancellationToken ct = default);
}

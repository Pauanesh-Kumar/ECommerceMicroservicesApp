using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Common;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Repositories;

namespace PaymentService.Infrastructure.Persistence;

public sealed class PaymentRepository(PaymentDbContext db) : IPaymentRepository
{
    public Task<Payment?> GetByOrderIdAsync(Guid orderId, CancellationToken ct = default)
        => db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.OrderId == orderId, ct);

    public Task<Payment?> GetByProviderOrderIdAsync(string providerOrderId, CancellationToken ct = default)
        => db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.ProviderOrderId == providerOrderId, ct);

    /// <summary>Keyset pagination on (CreatedAtUtc, Id) — same seek-predicate pattern as
    /// OrderService.Infrastructure.Persistence.OrderRepository.GetByCursorAsync.</summary>
    public async Task<CursorPageResult<Payment>> GetByCursorAsync(string? cursor, int limit, Guid? userId, CancellationToken ct = default)
    {
        var query = db.Payments.AsNoTracking().AsQueryable();
        if (userId is { } uid) query = query.Where(p => p.UserId == uid);

        var decoded = CursorEncoder.Decode(cursor);
        if (decoded is { } key)
        {
            query = query.Where(p =>
                p.CreatedAtUtc > key.CreatedAtUtc ||
                (p.CreatedAtUtc == key.CreatedAtUtc && p.Id.CompareTo(key.Id) > 0));
        }

        var rows = await query.OrderBy(p => p.CreatedAtUtc).ThenBy(p => p.Id).Take(limit + 1).ToListAsync(ct);
        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;
        var nextCursor = page.Count > 0 ? CursorEncoder.Encode(page[^1].CreatedAtUtc, page[^1].Id) : null;

        return new CursorPageResult<Payment>(page, hasMore ? nextCursor : null, hasMore);
    }
}

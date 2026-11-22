using MediatR;
using PaymentService.Application.Common;
using PaymentService.Domain.Entities;
using PaymentService.Domain.Repositories;

namespace PaymentService.Application.Queries.GetPayments;

public sealed record GetPaymentsQuery(string? Cursor, int Limit, Guid? UserId) : IRequest<CursorPage<Payment>>;

public sealed class GetPaymentsQueryHandler(IPaymentRepository repo) : IRequestHandler<GetPaymentsQuery, CursorPage<Payment>>
{
    public async Task<CursorPage<Payment>> Handle(GetPaymentsQuery query, CancellationToken ct)
    {
        var limit = query.Limit is <= 0 or > 100 ? 20 : query.Limit;
        var result = await repo.GetByCursorAsync(query.Cursor, limit, query.UserId, ct);
        return new CursorPage<Payment>(result.Items, result.NextCursor, result.HasMore);
    }
}

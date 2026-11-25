using MediatR;
using OrderService.Application.Commands;
using OrderService.Application.Queries.GetOrders;
using OrderService.Domain.Repositories;
using System.Security.Claims;

namespace OrderService.API.Endpoints;

public static class OrderEndpoints
{
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = 20;

    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Orders").WithOpenApi();

        orders.MapPost("/", async (HttpRequest http, CreateOrderApiRequest req, ISender mediator, CancellationToken ct) =>
        {
            var idempotencyKey = http.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return Results.BadRequest(new { error = "Idempotency-Key header is required." });

            if (idempotencyKey.Length > 100)
                return Results.BadRequest(new { error = "Idempotency-Key must be at most 100 characters." });

            if (req.Items is not { Count: > 0 and <= 100 } ||
                req.Items.Any(i => i.ProductId == Guid.Empty || i.Quantity < 1 || i.Quantity > 1000 || i.UnitPrice <= 0 || string.IsNullOrWhiteSpace(i.Name) || i.Name.Length > 200))
                return Results.BadRequest(new { error = "Order needs 1-100 valid items (productId, name, quantity 1-1000, unitPrice > 0)." });

            var sub = http.HttpContext.User.FindFirst("sub")?.Value ?? http.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sub, out var jwtUserId))
                return Results.Unauthorized();

            var result = await mediator.Send(new CreateOrderCommand(
                jwtUserId,
                req.Items.Select(i => new CreateOrderLine(i.ProductId, i.Name, i.UnitPrice, i.Quantity)).ToList(),
                idempotencyKey,
                http.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value ?? http.HttpContext.User.FindFirst("email")?.Value,
                http.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? http.HttpContext.User.FindFirst("name")?.Value), ct);

            return result.WasAlreadyProcessed
                ? Results.Ok(new { orderId = result.OrderId })
                : Results.Created($"/api/orders/{result.OrderId}", new { orderId = result.OrderId });
        })
        .RequireAuthorization("User")
        .WithSummary("Create an order (idempotent via Idempotency-Key header) — kicks off the fulfilment saga")
        .WithOpenApi();

        orders.MapGet("/user/{userId}", GetOrdersForUser)
            .RequireAuthorization("User")
            .WithName("GetUserOrders")
            .WithSummary("Get all orders for a user (keyset pagination)")
            .WithDescription("""
                Keyset cursor-based pagination for efficient stable data retrieval.
                
                Parameters:
                - userId: The user ID (path parameter)
                - cursor: Base64-encoded cursor from previous response (optional, null for first page)
                - limit: Page size (1-50, default: 20)
                
                Response includes 'nextCursor' for fetching the next page.
                The cursor is stable across data changes (insertions/deletions).
                """)
            .WithOpenApi();

        return app;
    }

    private static async Task<IResult> GetOrdersForUser(
        Guid userId,
        string? cursor,
        int? limit,
        ClaimsPrincipal caller,
        ISender mediator,
        CancellationToken ct)
    {
        var sub = caller.FindFirst("sub")?.Value ?? caller.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!caller.IsInRole("Admin") && !string.Equals(sub, userId.ToString(), StringComparison.OrdinalIgnoreCase))
            return Results.Forbid();

        // Validate and clamp page size
        var pageSize = Math.Min(limit ?? DefaultPageSize, MaxPageSize);
        if (pageSize < 1) pageSize = DefaultPageSize;

        var result = await mediator.Send(
            new GetOrdersQuery(cursor, pageSize, userId), ct);

        return Results.Ok(new PagedResponse<OrderDto>(
            result.Orders,
            result.NextCursor,
            result.HasMore,
            result.TotalCount));
    }
}

public sealed record CreateOrderApiRequest(Guid UserId, List<OrderItemDto> Items);
public sealed record OrderItemDto(Guid ProductId, string Name, decimal UnitPrice, int Quantity);

/// <summary>
/// Standard paginated response wrapper for keyset/cursor pagination.
/// The NextCursor should be passed as 'cursor' parameter in the next request.
/// </summary>
public sealed record PagedResponse<T>(
    IEnumerable<T> Data,
    string? NextCursor,
    bool HasMore,
    int TotalCount);
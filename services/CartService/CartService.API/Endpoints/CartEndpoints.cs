using CartService.Application.Commands.Checkout;
using CartService.Domain.Entities;
using CartService.Domain.Repositories;
using MediatR;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace CartService.API.Endpoints;

public static class CartEndpoints
{
    public static IEndpointRouteBuilder MapCartEndpoints(this IEndpointRouteBuilder app)
    {
        var cart = app.MapGroup("/api/cart").WithTags("Cart").WithOpenApi().RequireAuthorization("User");

        cart.MapGet("/", async (ClaimsPrincipal user, IRedisCartRepository repo, CancellationToken ct) =>
            Results.Ok(await repo.GetAsync(GetUserId(user), ct)));

        cart.MapPost("/items", async (ClaimsPrincipal user, AddCartItemRequest req, IRedisCartRepository repo, CancellationToken ct) =>
        {
            if (req.ProductId == Guid.Empty || req.Quantity < 1 || req.Quantity > 1000 || req.UnitPrice <= 0 || string.IsNullOrWhiteSpace(req.Name) || req.Name.Length > 200)
                return Results.BadRequest(new { error = "Invalid cart item (productId, name, quantity 1-1000, unitPrice > 0)." });

            var userId = GetUserId(user);
            var existing = await repo.GetAsync(userId, ct);
            var others = existing.Items.Where(i => i.ProductId != req.ProductId).ToList();
            others.Add(new CartItem(req.ProductId, req.Name, req.UnitPrice, req.Quantity));
            var updated = existing with { Items = others };
            await repo.SaveAsync(updated, ct);
            return Results.Ok(updated);
        });

        cart.MapDelete("/items/{productId:guid}", async (ClaimsPrincipal user, Guid productId, IRedisCartRepository repo, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            var existing = await repo.GetAsync(userId, ct);
            var updated = existing with { Items = existing.Items.Where(i => i.ProductId != productId).ToList() };
            await repo.SaveAsync(updated, ct);
            return Results.Ok(updated);
        });

        cart.MapPost("/checkout", async (ClaimsPrincipal user, HttpRequest http, ISender mediator, CancellationToken ct) =>
        {
            if (!AuthenticationHeaderValue.TryParse(http.Headers.Authorization.ToString(), out var authorization)
                || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(authorization.Parameter))
                return Results.Unauthorized();

            var idempotencyKey = http.Headers["Idempotency-Key"].FirstOrDefault() ?? Guid.NewGuid().ToString();
            var result = await mediator.Send(new CheckoutCommand(GetUserId(user), idempotencyKey, authorization.Parameter), ct);
            return result.Success ? Results.Ok(new { orderId = result.OrderId }) : Results.BadRequest(new { error = result.Error });
        });

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException();
    }
}

public sealed record AddCartItemRequest(Guid ProductId, string Name, decimal UnitPrice, int Quantity);

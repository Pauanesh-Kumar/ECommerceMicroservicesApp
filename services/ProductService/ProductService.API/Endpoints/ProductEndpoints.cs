using MediatR;
using ProductService.Application.Commands.CreateProduct;
using ProductService.Application.Queries.GetProducts;
using ProductService.Domain.Repositories;

namespace ProductService.API.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/api/products").WithTags("Products").WithOpenApi();

        products.MapGet("/{id:guid}", async (Guid id, IProductRepository repo, CancellationToken ct) =>
        {
            var product = await repo.GetByIdAsync(id, ct);
            return product is null ? Results.NotFound() : Results.Ok(product);
        }).WithSummary("Get product by id");

        // Cursor/keyset pagination: pass `cursor` (opaque, from the previous response's
        // nextCursor) to fetch the next page. Omit `cursor` for the first page. Never
        // pass a page NUMBER — there isn't one; this is not offset pagination.
        products.MapGet("/", async (string? cursor, int? limit, string? category, ISender mediator, CancellationToken ct) =>
        {
            var page = await mediator.Send(new GetProductsQuery(cursor, limit ?? 20, category), ct);
            return Results.Ok(new { items = page.Items, nextCursor = page.NextCursor, hasMore = page.HasMore });
        })
        .WithSummary("Cursor-paginated product catalog listing (keyset pagination on CreatedAtUtc+Id)");

        products.MapPost("/", async (CreateProductRequest req, ISender mediator, CancellationToken ct) =>
        {
            var id = await mediator.Send(new CreateProductCommand(
                req.Sku, req.Name, req.Description, req.Category, req.Price, req.Currency, req.InitialStock), ct);
            return Results.Created($"/api/products/{id}", new { productId = id });
        })
        .RequireAuthorization("Admin")
        .WithSummary("Create a new catalog product (admin only)");

        return app;
    }
}

public sealed record CreateProductRequest(
    string Sku, string Name, string Description, string Category, decimal Price, string Currency, int InitialStock);

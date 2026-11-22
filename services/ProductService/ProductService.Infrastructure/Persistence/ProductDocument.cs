using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using ProductService.Domain.Entities;
using System;

namespace ProductService.Infrastructure.Persistence;

internal sealed class ProductDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; init; }

    public string Sku { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = default!;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "INR";

    public int Stock { get; init; }
    public int ReservedStock { get; init; }

    public long Version { get; init; }

    public bool IsActive { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    public Product ToDomain() => new Product
    {
        Id = Id,
        Sku = Sku,
        Name = Name,
        Description = Description,
        Category = Category,
        Price = Price,
        Currency = Currency,
        Stock = Stock,
        ReservedStock = ReservedStock,
        Version = Version,
        IsActive = IsActive,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc
    };

    public static ProductDocument FromDomain(Product p) => new ProductDocument
    {
        Id = p.Id,
        Sku = p.Sku,
        Name = p.Name,
        Description = p.Description,
        Category = p.Category,
        Price = p.Price,
        Currency = p.Currency,
        Stock = p.Stock,
        ReservedStock = p.ReservedStock,
        Version = p.Version,
        IsActive = p.IsActive,
        CreatedAtUtc = p.CreatedAtUtc,
        UpdatedAtUtc = p.UpdatedAtUtc
    };
}
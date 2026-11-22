using System;

namespace ProductService.Domain.Entities;

/// <summary>
/// Catalog aggregate. Stock and ReservedStock are separate counters so
/// "available to sell" = Stock; a reservation atomically moves units from
/// Stock into ReservedStock (see repository implementation).
/// </summary>
public sealed class Product
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Sku { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = default!;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "INR";

    public int Stock { get; init; }
    public int ReservedStock { get; init; }

    /// <summary>Optimistic-concurrency token for non-stock edits.</summary>
    public long Version { get; init; }

    public bool IsActive { get; init; } = true;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public static Product Create(string sku, string name, string description, string category,
        decimal price, string currency, int initialStock) => new()
    {
        Sku = sku,
        Name = name,
        Description = description,
        Category = category,
        Price = price,
        Currency = currency,
        Stock = initialStock,
        ReservedStock = 0,
        Version = 0
    };
}

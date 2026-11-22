namespace OrderService.Domain.Entities;

public sealed class OrderItem
{
    private OrderItem() { } // EF Core

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public static OrderItem Create(Guid productId, string productName, decimal unitPrice, int quantity) => new()
    {
        ProductId = productId, ProductName = productName, UnitPrice = unitPrice, Quantity = quantity
    };
}

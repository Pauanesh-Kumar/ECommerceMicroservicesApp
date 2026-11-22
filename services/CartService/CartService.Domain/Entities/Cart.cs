namespace CartService.Domain.Entities;

public sealed record CartItem(Guid ProductId, string Name, decimal UnitPrice, int Quantity);

public sealed record Cart(Guid UserId, List<CartItem> Items)
{
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);
}

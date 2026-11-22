namespace OrderService.Domain.Entities;

public enum OrderStatus { Pending, AwaitingPayment, Confirmed, Cancelled, Failed }

public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    private Order() { } // EF Core

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "INR";
    public string? CancellationReason { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Postgres system column `xmin` mapped as an EF Core concurrency token —
    /// no extra column, no extra write, automatically bumped by Postgres on every
    /// UPDATE. Two concurrent status transitions on the same row throw
    /// DbUpdateConcurrencyException.</summary>
    public uint RowVersion { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items;

    public static Order Create(Guid userId, IEnumerable<OrderItem> items)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        order._items.AddRange(items);
        order.TotalAmount = order._items.Sum(i => i.UnitPrice * i.Quantity);
        return order;
    }

    public void MarkAwaitingPayment()
    {
        if (Status != OrderStatus.Pending) throw new InvalidOperationException($"Cannot move to AwaitingPayment from {Status}.");
        Status = OrderStatus.AwaitingPayment;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.AwaitingPayment) throw new InvalidOperationException($"Cannot confirm from {Status}.");
        Status = OrderStatus.Confirmed;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        if (Status is OrderStatus.Confirmed or OrderStatus.Cancelled) throw new InvalidOperationException($"Cannot cancel from {Status}.");
        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

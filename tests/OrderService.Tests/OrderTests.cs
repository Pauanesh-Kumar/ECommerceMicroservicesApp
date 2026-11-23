using OrderService.Application.Commands;
using OrderService.Domain.Entities;

namespace OrderService.Tests;

public class OrderTests
{
    [Fact]
    public void OrderTotalsLinesAndEnforcesStatusTransitions()
    {
        var order = Order.Create(Guid.NewGuid(), [OrderItem.Create(Guid.NewGuid(), "Item", 12.5m, 3)]);
        Assert.Equal(37.5m, order.TotalAmount);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Throws<InvalidOperationException>(order.Confirm);
        order.MarkAwaitingPayment();
        order.Confirm();
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Throws<InvalidOperationException>(() => order.Cancel("late"));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void ValidatorRejectsInvalidLines(int price, int quantity)
    {
        var cmd = new CreateOrderCommand(Guid.NewGuid(), [new(Guid.NewGuid(), "Item", price, quantity)], "key");
        Assert.False(new CreateOrderCommandValidator().Validate(cmd).IsValid);
    }

    [Fact]
    public void ValidatorRequiresIdempotencyKeyAndItems()
    {
        var result = new CreateOrderCommandValidator().Validate(new CreateOrderCommand(Guid.NewGuid(), [], ""));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Items");
        Assert.Contains(result.Errors, error => error.PropertyName == "IdempotencyKey");
    }
}

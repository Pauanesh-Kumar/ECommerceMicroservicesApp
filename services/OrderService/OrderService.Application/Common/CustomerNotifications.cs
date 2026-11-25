using OrderService.Application.Interfaces;
using OrderService.Contracts;
using OrderService.Domain.Entities;
using System.Text.Json;

namespace OrderService.Application.Common;

/// <summary>
/// Enqueues customer-facing notification events into the same outbox transaction as the order
/// state change. Skipped when no customer email was captured, because NotificationService cannot
/// deliver without one.
/// </summary>
internal static class CustomerNotifications
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void OrderPlaced(IOrderUnitOfWork uow, Order order)
    {
        if (!HasContact(order)) return;
        var (first, last) = SplitName(order.CustomerName);
        uow.EnqueueOutboxMessage(NotificationTopics.OrderPlaced, order.Id.ToString(), JsonSerializer.Serialize(
            new OrderPlacedNotificationEvent("OrderPlaced", order.Id, order.UserId, order.CustomerEmail!, first, last,
                order.TotalAmount, order.Currency, DateTime.UtcNow), Json));
    }

    public static void PaymentProcessed(IOrderUnitOfWork uow, Order order, Guid paymentId)
    {
        if (!HasContact(order)) return;
        var (first, last) = SplitName(order.CustomerName);
        uow.EnqueueOutboxMessage(NotificationTopics.PaymentProcessed, order.Id.ToString(), JsonSerializer.Serialize(
            new PaymentNotificationEvent("PaymentProcessed", paymentId, order.Id, order.UserId, order.CustomerEmail!, first, last,
                order.TotalAmount, order.Currency, true, null, DateTime.UtcNow), Json));
    }

    public static void PaymentFailed(IOrderUnitOfWork uow, Order order, string reason)
    {
        if (!HasContact(order)) return;
        var (first, last) = SplitName(order.CustomerName);
        uow.EnqueueOutboxMessage(NotificationTopics.PaymentProcessed, order.Id.ToString(), JsonSerializer.Serialize(
            new PaymentNotificationEvent("PaymentFailed", Guid.Empty, order.Id, order.UserId, order.CustomerEmail!, first, last,
                order.TotalAmount, order.Currency, false, reason, DateTime.UtcNow), Json));
    }

    private static bool HasContact(Order order) => !string.IsNullOrWhiteSpace(order.CustomerEmail);

    private static (string? First, string? Last) SplitName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return (null, null);
        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return (parts[0], parts.Length > 1 ? parts[1] : null);
    }
}

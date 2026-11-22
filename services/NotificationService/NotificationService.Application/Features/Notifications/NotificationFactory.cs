using NotificationService.Application.Features.Notifications.Common;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Application.Features.Notifications.Templates;

namespace NotificationService.Application.Features.Notifications;

/// <summary>
/// Drop-in replacement for the templated factories that used to live on the
/// Domain entity. Same names, same signatures — only the type qualifier changes
/// at call sites (Notification.CreateWelcome(...) → NotificationFactory.CreateWelcome(...)).
/// Keeps Domain dependency-free (Clean Architecture) while keeping template text
/// in exactly one place (DRY) instead of 11 duplicated method bodies.
/// </summary>
public static class NotificationFactory
{
    // ── User Service ─────────────────────────────────────────────────────────

    public static Notification CreateWelcome(Guid userId, string email, string fullName) =>
        FromTemplate(userId, email, NotificationEventType.User_Registered, new(FullName: fullName));

    public static Notification CreateUserUpdated(Guid userId, string email, string fullName) =>
        FromTemplate(userId, email, NotificationEventType.User_Updated, new(FullName: fullName));

    public static Notification CreateUserDeactivated(Guid userId, string email, string fullName) =>
        FromTemplate(userId, email, NotificationEventType.User_Deactivated, new(FullName: fullName));

    public static Notification CreateUserPasswordChanged(Guid userId, string email, string fullName) =>
        FromTemplate(userId, email, NotificationEventType.User_PasswordChanged, new(FullName: fullName));

    // ── Order Service ────────────────────────────────────────────────────────

    public static Notification CreateOrderPlaced(Guid userId, string email, string fullName, string orderId) =>
        FromTemplate(userId, email, NotificationEventType.Order_Placed, new(FullName: fullName, OrderId: orderId));

    public static Notification CreateOrderShipped(Guid userId, string email, string fullName, string orderId) =>
        FromTemplate(userId, email, NotificationEventType.Order_Shipped, new(FullName: fullName, OrderId: orderId));

    public static Notification CreateOrderDelivered(Guid userId, string email, string fullName, string orderId) =>
        FromTemplate(userId, email, NotificationEventType.Order_Delivered, new(FullName: fullName, OrderId: orderId));

    public static Notification CreateOrderCancelled(Guid userId, string email, string fullName, string orderId) =>
        FromTemplate(userId, email, NotificationEventType.Order_Cancelled, new(FullName: fullName, OrderId: orderId));

    // ── Payment Service ──────────────────────────────────────────────────────

    public static Notification CreatePaymentProcessed(Guid userId, string email, string fullName, string amount) =>
        FromTemplate(userId, email, NotificationEventType.Payment_Processed, new(FullName: fullName, Amount: amount));

    public static Notification CreatePaymentFailed(Guid userId, string email, string fullName, string amount) =>
        FromTemplate(userId, email, NotificationEventType.Payment_Failed, new(FullName: fullName, Amount: amount));

    public static Notification CreatePaymentRefunded(Guid userId, string email, string fullName, string amount) =>
        FromTemplate(userId, email, NotificationEventType.Payment_Refunded, new(FullName: fullName, Amount: amount));

    // ── Product Service ──────────────────────────────────────────────────────

    public static Notification CreateProductStockLow(Guid userId, string email, string productName) =>
        FromTemplate(userId, email, NotificationEventType.Product_StockLow, new(ProductName: productName));

    // ── Shared resolve-then-build step ──────────────────────────────────────

    private static Notification FromTemplate(
        Guid userId, string recipient, NotificationEventType eventType, NotificationTemplateData data)
    {
        var (subject, body) = NotificationTemplateRegistry.Resolve(eventType, data);
        return Notification.Create(userId, recipient, subject, body, NotificationChannel.Email, eventType);
    }
}


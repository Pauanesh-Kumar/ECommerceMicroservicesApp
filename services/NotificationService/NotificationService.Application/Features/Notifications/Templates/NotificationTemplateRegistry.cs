using NotificationService.Application.Features.Notifications.Common;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Features.Notifications.Templates
{
    public static class NotificationTemplateRegistry
    {
        // Key is now (EventType, Channel) — a single event (e.g. User_Registered)
        // can have different copy per channel (Email has a subject; SMS doesn't).
        private static readonly Dictionary<(NotificationEventType EventType, NotificationChannel Channel), (string? Subject, Func<NotificationTemplateData, string> Body)> Templates = new()
        {
            // ── Email ────────────────────────────────────────────────────────
            [(NotificationEventType.User_Registered, NotificationChannel.Email)] = ("Welcome to ECommerce!", d => $"Hi {d.FullName}, your account has been created. Welcome aboard!"),
            [(NotificationEventType.User_Updated, NotificationChannel.Email)] = ("Your account has been updated", d => $"Hi {d.FullName}, your account details were recently updated."),
            [(NotificationEventType.User_Deactivated, NotificationChannel.Email)] = ("Your account has been deactivated", d => $"Hi {d.FullName}, your account has been deactivated. Contact support if unexpected."),
            [(NotificationEventType.User_PasswordChanged, NotificationChannel.Email)] = ("Your password was changed", d => $"Hi {d.FullName}, your password was just changed. If this wasn't you, contact support immediately."),

            [(NotificationEventType.Order_Placed, NotificationChannel.Email)] = ("Order Placed Successfully", d => $"Hi {d.FullName}, your order #{d.OrderId} has been placed. We'll notify you when it ships."),
            [(NotificationEventType.Order_Shipped, NotificationChannel.Email)] = ("Your Order Has Shipped", d => $"Hi {d.FullName}, your order #{d.OrderId} is on its way!"),
            [(NotificationEventType.Order_Delivered, NotificationChannel.Email)] = ("Your Order Has Been Delivered", d => $"Hi {d.FullName}, your order #{d.OrderId} has been delivered. Enjoy!"),
            [(NotificationEventType.Order_Cancelled, NotificationChannel.Email)] = ("Your Order Has Been Cancelled", d => $"Hi {d.FullName}, your order #{d.OrderId} has been cancelled."),

            [(NotificationEventType.Payment_Processed, NotificationChannel.Email)] = ("Payment Successful", d => $"Hi {d.FullName}, your payment of {d.Amount} was processed successfully."),
            [(NotificationEventType.Payment_Failed, NotificationChannel.Email)] = ("Payment Failed", d => $"Hi {d.FullName}, your payment of {d.Amount} could not be processed. Please update your payment details."),
            [(NotificationEventType.Payment_Refunded, NotificationChannel.Email)] = ("Payment Refunded", d => $"Hi {d.FullName}, a refund of {d.Amount} has been initiated to your account."),

            [(NotificationEventType.Product_StockLow, NotificationChannel.Email)] = ("Low Stock Alert", d => $"The product '{d.ProductName}' is running low on stock."),

            // ── SMS — no subject; short-form body ───────────────────────────
            [(NotificationEventType.User_Registered, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, welcome to ECommerce! Your account is ready."),
            [(NotificationEventType.User_Updated, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, your account details were updated."),
            [(NotificationEventType.User_Deactivated, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, your account has been deactivated."),
            [(NotificationEventType.User_PasswordChanged, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, your password was changed."),
            [(NotificationEventType.Order_Placed, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, order #{d.OrderId} placed successfully."),
            [(NotificationEventType.Order_Shipped, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, order #{d.OrderId} has shipped!"),
            [(NotificationEventType.Payment_Failed, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, your payment of {d.Amount} failed."),
            [(NotificationEventType.Payment_Processed, NotificationChannel.Sms)] = (null, d => $"Hi {d.FullName}, payment of {d.Amount} received."),
        };

        public static (string? Subject, string Body) Resolve(
            NotificationEventType eventType, NotificationChannel channel, NotificationTemplateData data)
        {
            if (!Templates.TryGetValue((eventType, channel), out var template))
                throw new InvalidOperationException($"No notification template registered for {eventType}/{channel}.");

            return (template.Subject, template.Body(data));
        }
    }
}
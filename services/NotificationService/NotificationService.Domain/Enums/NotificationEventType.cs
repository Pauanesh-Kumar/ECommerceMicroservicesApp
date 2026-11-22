namespace NotificationService.Domain.Enums;

/// <summary>
/// Defines WHY the notification was triggered — the originating business event, grouped by service/module.
/// Convention: {Service}_{Action}  e.g. Order_Shipped, Payment_Failed.
///
/// This is NOT the delivery channel (see NotificationChannel).
/// One event can trigger multiple deliveries across multiple channels.
///
/// Add a new value here when a new integration event from any service is introduced.
/// No strategy, handler, or consumer file needs to change (OCP).
/// </summary>
public enum NotificationEventType
{
    // ── User Service ─────────────────────────────────────────────────────────
    User_Registered,
    User_Updated,
    User_Deactivated,
    User_PasswordChanged,
    User_EmailVerified,

    // ── Product Service ──────────────────────────────────────────────────────
    Product_Created,
    Product_Updated,
    Product_Deleted,
    Product_StockLow,
    Product_StockRestored,

    // ── Order Service ────────────────────────────────────────────────────────
    Order_Placed,
    Order_Confirmed,
    Order_Shipped,
    Order_Delivered,
    Order_Cancelled,
    Order_ReturnRequested,

    // ── Payment Service ──────────────────────────────────────────────────────
    Payment_Processed,
    Payment_Failed,
    Payment_Refunded,

    // ── Inventory Service ────────────────────────────────────────────────────
    Inventory_Updated,
    Inventory_OutOfStock,

    // ── Unknown / Unhandled ──────────────────────────────────────────────────
    Unknown = 999
}
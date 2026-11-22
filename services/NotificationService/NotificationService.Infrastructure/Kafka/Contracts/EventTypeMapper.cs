using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Kafka.Contracts;

/// <summary>
/// Maps raw Kafka EventType strings (from any service) → NotificationEventType enum.
/// Single place for all service→event mappings (OCP: strategies never change for new events).
/// Keys are case-insensitive; unmapped strings return NotificationEventType.Unknown.
/// </summary>
public static class EventTypeMapper
{
    private static readonly IReadOnlyDictionary<string, NotificationEventType> Map =
        new Dictionary<string, NotificationEventType>(StringComparer.OrdinalIgnoreCase)
        {
            // ── User Service ─────────────────────────────────────────────────
            { "UserRegistered",       NotificationEventType.User_Registered      },
            { "UserUpdated",          NotificationEventType.User_Updated          },
            { "UserDeactivated",      NotificationEventType.User_Deactivated      },
            { "UserPasswordChanged",  NotificationEventType.User_PasswordChanged  },
            { "UserEmailVerified",    NotificationEventType.User_EmailVerified    },

            // ── Product Service ──────────────────────────────────────────────
            { "ProductCreated",       NotificationEventType.Product_Created       },
            { "ProductUpdated",       NotificationEventType.Product_Updated       },
            { "ProductDeleted",       NotificationEventType.Product_Deleted       },
            { "ProductStockLow",      NotificationEventType.Product_StockLow      },
            { "ProductStockRestored", NotificationEventType.Product_StockRestored },

            // ── Order Service ────────────────────────────────────────────────
            { "OrderPlaced",          NotificationEventType.Order_Placed          },
            { "OrderConfirmed",       NotificationEventType.Order_Confirmed       },
            { "OrderShipped",         NotificationEventType.Order_Shipped         },
            { "OrderDelivered",       NotificationEventType.Order_Delivered       },
            { "OrderCancelled",       NotificationEventType.Order_Cancelled       },
            { "OrderReturnRequested", NotificationEventType.Order_ReturnRequested },

            // ── Payment Service ──────────────────────────────────────────────
            { "PaymentProcessed",     NotificationEventType.Payment_Processed     },
            { "PaymentFailed",        NotificationEventType.Payment_Failed        },
            { "PaymentRefunded",      NotificationEventType.Payment_Refunded      },

            // ── Inventory Service ────────────────────────────────────────────
            { "InventoryUpdated",     NotificationEventType.Inventory_Updated     },
            { "InventoryOutOfStock",  NotificationEventType.Inventory_OutOfStock  },
        };

    public static NotificationEventType Resolve(string rawEventType)
        => Map.TryGetValue(rawEventType, out var mapped) ? mapped : NotificationEventType.Unknown;
}
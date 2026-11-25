using System.Text.Json;

namespace AnalyticsService.Infrastructure.Reporting;

public abstract record ReportingChange;
public sealed record OrderCreatedChange(Guid OrderId, Guid? UserId, DateTime AtUtc) : ReportingChange;
public sealed record OrderConfirmedChange(Guid OrderId, Guid? UserId, decimal Amount, DateTime AtUtc) : ReportingChange;
public sealed record OrderCancelledChange(Guid OrderId, Guid? UserId, DateTime AtUtc) : ReportingChange;
public sealed record PaymentChange(Guid PaymentId, Guid OrderId, string Status, DateTime AtUtc) : ReportingChange;

/// <summary>
/// Pure translation of a bronze-layer event into a reporting-model change (no I/O, unit-testable).
/// The consumer stores the topic name as the event type for direct-shape topics (order-created,
/// order-confirmed, order-cancelled) and the envelope's eventType for envelope topics (payment-status),
/// so both spellings are matched.
/// </summary>
public static class ReportingEventMapper
{
    public static ReportingChange? Map(string eventType, string rawPayload, DateTime ingestedAtUtc)
    {
        JsonDocument? outer = null, inner = null;
        try
        {
            outer = JsonDocument.Parse(rawPayload);
            var root = outer.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var body = root;
            if (TryGet(root, "payload", out var p))
            {
                if (p.ValueKind == JsonValueKind.String)
                {
                    inner = JsonDocument.Parse(p.GetString() ?? "{}");
                    body = inner.RootElement;
                }
                else if (p.ValueKind == JsonValueKind.Object) body = p;
            }
            if (body.ValueKind != JsonValueKind.Object) return null;

            var at = ReadTime(body) ?? ingestedAtUtc;
            var orderId = ReadGuid(body, "orderId");
            var userId = ReadGuid(body, "userId");

            switch (Normalize(eventType))
            {
                case "ordercreated":
                    return orderId is { } c ? new OrderCreatedChange(c, userId, at) : null;
                case "orderconfirmed":
                    return orderId is { } f ? new OrderConfirmedChange(f, userId, ReadDecimal(body, "amount") ?? ReadDecimal(body, "totalAmount") ?? 0m, at) : null;
                case "ordercancelled":
                    return orderId is { } x ? new OrderCancelledChange(x, userId, at) : null;
                case "paymentprocessed":
                    return Payment(body, orderId, "Succeeded", at);
                case "paymentfailed":
                    return Payment(body, orderId, "Failed", at);
                default:
                    return null;
            }
        }
        catch (JsonException) { return null; }
        finally { inner?.Dispose(); outer?.Dispose(); }
    }

    private static PaymentChange? Payment(JsonElement body, Guid? orderId, string status, DateTime at)
        => ReadGuid(body, "paymentId") is { } paymentId && orderId is { } o ? new PaymentChange(paymentId, o, status, at) : null;

    internal static string Normalize(string value)
        => value.Replace("-", "").Replace("_", "").Replace(".", "").ToLowerInvariant().Replace("integrationevent", "");

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) { value = prop.Value; return true; }
        value = default;
        return false;
    }

    private static Guid? ReadGuid(JsonElement obj, string name)
        => TryGet(obj, name, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetGuid(out var g) && g != Guid.Empty ? g : null;

    private static decimal? ReadDecimal(JsonElement obj, string name)
        => TryGet(obj, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d) ? d : null;

    private static DateTime? ReadTime(JsonElement obj)
    {
        foreach (var name in new[] { "occurredOnUtc", "occurredOn" })
            if (TryGet(obj, name, out var v) && v.ValueKind == JsonValueKind.String && v.TryGetDateTime(out var dt))
                return dt.Kind == DateTimeKind.Utc ? dt : dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }
}

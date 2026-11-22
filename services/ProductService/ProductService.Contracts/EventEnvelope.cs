namespace ProductService.Contracts;

/// <summary>
/// Wire envelope for shared/multi-shape Kafka topics (product-stock, payment-status —
/// each topic carries two distinct event shapes: success/failure). Every publisher on
/// such a topic wraps its payload as EventEnvelope{EventType, Payload}; consumers read
/// EventType to pick the right DTO/strategy before deserializing Payload.
///
/// Contracts is intentionally its OWN project, outside API/Application/Domain/
/// Infrastructure — these records are the wire contract with OTHER services, not
/// internal application logic, so they must not be coupled to (or accidentally expose)
/// this service's Domain/Application internals. Convention carried over from
/// UserService/NotificationService: keep each service's copy of a shared event
/// field-for-field identical (see comments on the event records below); promote to a
/// shared NuGet package only if you want compile-time enforcement instead of convention.
/// </summary>
public sealed record EventEnvelope(string EventType, string Payload);

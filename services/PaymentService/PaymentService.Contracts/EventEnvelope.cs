namespace PaymentService.Contracts;

/// <summary>Wire envelope for the payment-status topic (two shapes: succeeded/failed) —
/// see ProductService.Contracts.EventEnvelope for the full rationale.</summary>
public sealed record EventEnvelope(string EventType, string Payload);

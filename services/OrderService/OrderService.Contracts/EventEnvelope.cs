namespace OrderService.Contracts;

/// <summary>Wire envelope for shared/multi-shape Kafka topics — see
/// ProductService.Contracts.EventEnvelope for the full rationale. Contracts is its own
/// project, outside API/Application/Domain/Infrastructure, because these types are the
/// wire contract with OTHER services, not this service's internal application logic.</summary>
public sealed record EventEnvelope(string EventType, string Payload);

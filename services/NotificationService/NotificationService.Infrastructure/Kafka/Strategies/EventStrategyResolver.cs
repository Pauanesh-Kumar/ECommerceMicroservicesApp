using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Kafka.Strategies;

/// <summary>
/// Resolves IEventStrategy by NotificationEventType.
/// Injected as scoped; strategies registered via IEnumerable&lt;IEventStrategy&gt; (LSP-safe collection).
/// DI provides all registered strategies — resolver just builds the lookup.
/// </summary>
public sealed class EventStrategyResolver
{
    private readonly IReadOnlyDictionary<NotificationEventType, IEventStrategy> _map;

    public EventStrategyResolver(IEnumerable<IEventStrategy> strategies)
        => _map = strategies.ToDictionary(s => s.EventType);

    /// <returns>Matching strategy, or null if no strategy registered for this event type.</returns>
    public IEventStrategy? Resolve(NotificationEventType eventType)
        => _map.TryGetValue(eventType, out var strategy) ? strategy : null;
}

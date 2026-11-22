namespace OrderService.Infrastructure.Kafka.Strategies;

public sealed class OrderEventStrategyResolver
{
    private readonly IReadOnlyDictionary<string, IOrderEventStrategy> _map;
    public OrderEventStrategyResolver(IEnumerable<IOrderEventStrategy> strategies) => _map = strategies.ToDictionary(s => s.Topic);
    public IOrderEventStrategy? Resolve(string topic, string eventType) => _map.TryGetValue($"{topic}:{eventType}", out var s) ? s : null;
}

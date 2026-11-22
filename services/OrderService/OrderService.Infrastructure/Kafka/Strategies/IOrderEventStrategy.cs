namespace OrderService.Infrastructure.Kafka.Strategies;

/// <summary>Same shape as NotificationService.Infrastructure.Kafka.Strategies.IEventStrategy —
/// one implementation per saga-reaction topic+eventType this service consumes.</summary>
public interface IOrderEventStrategy
{
    string Topic { get; }
    Task HandleAsync(string rawJson, CancellationToken ct);
}

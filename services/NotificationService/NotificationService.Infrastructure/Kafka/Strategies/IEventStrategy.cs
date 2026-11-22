using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Kafka.Strategies;

/// <summary>
/// Strategy interface — one implementation per NotificationEventType value.
/// Adding a new integration event = add one class, zero changes to consumers or DI (OCP).
/// </summary>
public interface IEventStrategy
{
    /// <summary>The NotificationEventType this strategy handles.</summary>
    NotificationEventType EventType { get; }

    Task HandleAsync(object integrationEvent, CancellationToken ct);
}

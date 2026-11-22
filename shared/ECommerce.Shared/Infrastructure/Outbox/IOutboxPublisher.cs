using ECommerce.Shared.Domain;

namespace ECommerce.Shared.Infrastructure.Outbox;

/// <summary>
/// Publishes outbox messages to Kafka with retry and DLQ logic.
/// Interface for the OutboxPublisher class, allowing for dependency injection and mocking in unit tests.
/// </summary>
public interface IOutboxPublisher
{
    Task PublishAsync(IEnumerable<OutboxMessage> messages, CancellationToken cancellationToken = default);
    Task<bool> PublishSingleAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}


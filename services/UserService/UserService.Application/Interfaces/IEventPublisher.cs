namespace UserService.Application.Interfaces;

// Generic outbound integration-event publisher abstraction, so Application-layer
// command handlers don't take a direct dependency on Confluent.Kafka.
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(string topic, TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : class;
}

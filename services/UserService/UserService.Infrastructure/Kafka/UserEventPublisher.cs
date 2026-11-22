using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using UserService.Application.Interfaces;

namespace UserService.Infrastructure.Kafka;

/// <summary>
/// Kafka implementation of IUserEventPublisher.
/// Publishes integration events as JSON to well-known topics.
///
/// SOLID — Single Responsibility: only event publishing logic here.
/// Open/Closed: new events are added as new methods without modifying existing ones.
/// </summary>
public sealed class UserEventPublisher(
    IProducer<string, string> producer,
    IOptions<KafkaOptions> options,
    ILogger<UserEventPublisher> logger)
    : IUserEventPublisher, IDisposable
{
    private readonly KafkaOptions _opts = options.Value;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Task PublishUserRegisteredAsync(
        Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
        => PublishAsync(_opts.UserUpdatedTopic, userId.ToString(), new
        {
            EventType = "UserRegistered",
            UserId = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            OccurredOn = DateTime.UtcNow
        }, ct);

    public Task PublishUserUpdatedAsync(
        Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
        => PublishAsync(_opts.UserUpdatedTopic, userId.ToString(), new
        {
            EventType = "UserUpdated",
            UserId = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            OccurredOn = DateTime.UtcNow
        }, ct);

    // Fixed: was (Guid userId) only — now matches the interface (email/firstName/lastName/phoneNumber),
    // so NotificationService's consumer can actually build a deactivation notification.
    public Task PublishUserDeactivatedAsync(
        Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
        => PublishAsync(_opts.UserUpdatedTopic, userId.ToString(), new
        {
            EventType = "UserDeactivated",
            UserId = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            OccurredOn = DateTime.UtcNow
        }, ct);

    // Added: was missing entirely — the class didn't compile against IUserEventPublisher without it.
    public Task PublishUserPasswordChangedAsync(
        Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
        => PublishAsync(_opts.UserUpdatedTopic, userId.ToString(), new
        {
            EventType = "UserPasswordChanged",
            UserId = userId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PhoneNumber = phoneNumber,
            OccurredOn = DateTime.UtcNow
        }, ct);

    // ── Private ───────────────────────────────────────────────────────────────
    private async Task PublishAsync(string topic, string key, object payload, CancellationToken ct)
    {
        var value = JsonSerializer.Serialize(payload, _json);
        var msg = new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers = new Headers
            {
                { "eventId",    System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) },
                { "occurredOn", System.Text.Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) }
            }
        };

        try
        {
            var result = await producer.ProduceAsync(topic, msg, ct);
            logger.LogDebug("Event published — topic={Topic} partition={P} offset={O}",
                topic, result.Partition.Value, result.Offset.Value);
        }
        catch (Exception ex)
        {
            // Log but don't throw — Kafka failure must NOT roll back user operations
            logger.LogError(ex, "Kafka publish failed — topic={Topic} key={Key}", topic, key);
        }
    }

    public void Dispose() => producer.Dispose();
}

/// <summary>Strongly-typed Kafka options from appsettings / env vars.</summary>
public sealed class KafkaOptions
{
    public const string Section = "Kafka";
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string UserUpdatedTopic { get; init; } = "user-updated";
}
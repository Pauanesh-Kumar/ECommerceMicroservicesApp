using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Persistence.MongoDb;

/// <summary>
/// MongoDB BSON document model.
/// Lives in Infrastructure — the Domain entity has zero BSON dependencies.
/// </summary>
internal sealed class NotificationDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; init; }

    [BsonRepresentation(BsonType.String)]
    public Guid UserId { get; init; }

    public string Recipient { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    public NotificationChannel Channel { get; init; }

    [BsonRepresentation(BsonType.String)]
    public NotificationEventType EventType { get; init; }

    [BsonRepresentation(BsonType.String)]
    public NotificationStatus Status { get; init; }

    public string? ErrorMessage { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? SentAtUtc { get; init; }
}
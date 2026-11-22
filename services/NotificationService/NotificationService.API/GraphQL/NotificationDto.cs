using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Api.GraphQL;

public sealed class NotificationDto
{
    public Guid Id { get; init; }
    public string Recipient { get; init; } = default!;
    public string Subject { get; init; } = default!;
    public NotificationChannel Channel { get; init; }
    public NotificationEventType EventType { get; init; }
    public NotificationStatus Status { get; init; }
    public DateTime CreatedAtUtc { get; init; }

    public static NotificationDto FromDomain(Notification n) => new()
    {
        Id = n.Id,
        Recipient = n.Recipient,
        Subject = n.Subject,
        Channel = n.Channel,
        EventType = n.EventType,
        Status = n.Status,
        CreatedAtUtc = n.CreatedAtUtc
    };
}

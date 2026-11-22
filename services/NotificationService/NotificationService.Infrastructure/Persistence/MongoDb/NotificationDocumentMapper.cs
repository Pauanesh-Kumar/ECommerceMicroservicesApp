using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Persistence.MongoDb;

internal static class NotificationDocumentMapper
{
    internal static NotificationDocument ToDocument(this Notification n) => new()
    {
        Id = n.Id,
        UserId = n.UserId,
        Recipient = n.Recipient,
        Subject = n.Subject,
        Body = n.Body,
        Channel = n.Channel,
        EventType = n.EventType,
        Status = n.Status,
        ErrorMessage = n.ErrorMessage,
        CreatedAtUtc = n.CreatedAtUtc,
        SentAtUtc = n.SentAtUtc
    };

    internal static Notification ToDomain(this NotificationDocument d) =>
        Notification.Reconstitute(
            d.Id, d.UserId, d.Recipient, d.Subject, d.Body,
            d.Channel, d.EventType, d.Status, d.ErrorMessage,
            d.CreatedAtUtc, d.SentAtUtc);
}

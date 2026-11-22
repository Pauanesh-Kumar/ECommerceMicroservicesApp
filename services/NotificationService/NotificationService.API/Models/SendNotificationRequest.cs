using NotificationService.Domain.Enums;

namespace NotificationService.API.Models;

public sealed record SendNotificationRequest(
    Guid UserId,
    string Recipient,
    string Subject,
    string Body,
    NotificationChannel Channel,
    NotificationEventType EventType);
using MediatR;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Features.Notifications.Commands.SendNotification;

public sealed record SendNotificationCommand(
    Guid UserId,
    string Recipient,
    string Subject,
    string Body,
    NotificationChannel Channel,
    NotificationEventType EventType) : IRequest<Guid>;



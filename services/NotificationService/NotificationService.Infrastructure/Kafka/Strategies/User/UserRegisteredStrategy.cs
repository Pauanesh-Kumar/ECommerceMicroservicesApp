using MediatR;
using NotificationService.Application.Features.Notifications.Commands.UserNotification;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Kafka.Contracts;

namespace NotificationService.Infrastructure.Kafka.Strategies.User;

public sealed class UserRegisteredStrategy(IMediator mediator) : IEventStrategy
{
    public NotificationEventType EventType => NotificationEventType.User_Registered;

    public Task HandleAsync(object integrationEvent, CancellationToken ct)
    {
        var ev = (UserIntegrationEvent)integrationEvent;
        return mediator.Send(new SendUserRegisteredNotificationCommand(
            ev.UserId,
            ev.Email ?? string.Empty,
            ev.FirstName ?? string.Empty,
            ev.LastName ?? string.Empty,
            ev.PhoneNumber), ct);
    }
}

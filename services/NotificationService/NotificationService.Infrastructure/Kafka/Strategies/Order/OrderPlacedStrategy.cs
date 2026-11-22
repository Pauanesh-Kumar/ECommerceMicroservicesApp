using MediatR;
using NotificationService.Application.Features.Notifications.Commands.OrderNotification;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Kafka.Contracts;

namespace NotificationService.Infrastructure.Kafka.Strategies.Order;

public sealed class OrderPlacedStrategy(IMediator mediator) : IEventStrategy
{
    public NotificationEventType EventType => NotificationEventType.Order_Placed;

    public Task HandleAsync(object integrationEvent, CancellationToken ct)
    {
        var ev = (OrderIntegrationEvent)integrationEvent;
        var fullName = $"{ev.FirstName} {ev.LastName}".Trim();

        return mediator.Send(new SendOrderPlacedNotificationCommand(
            ev.UserId,
            ev.Email ?? string.Empty,
            fullName,
            ev.OrderId.ToString(), ev.PhoneNumber ?? string.Empty), ct);
    }
}
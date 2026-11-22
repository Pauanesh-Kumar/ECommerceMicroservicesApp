using MediatR;
using NotificationService.Application.Features.Notifications.Commands.OrderNotification;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Kafka.Contracts;

namespace NotificationService.Infrastructure.Kafka.Strategies.Order;

public sealed class OrderShippedStrategy(IMediator mediator) : IEventStrategy
{
    public NotificationEventType EventType => NotificationEventType.Order_Shipped;

    public Task HandleAsync(object integrationEvent, CancellationToken ct)
    {
        var ev = (OrderIntegrationEvent)integrationEvent;
        var fullName = $"{ev.FirstName} {ev.LastName}".Trim();

        return mediator.Send(new SendOrderShippedNotificationCommand(
            ev.UserId,
            ev.Email ?? string.Empty,
            fullName,
            ev.OrderId.ToString(),
            ev.PhoneNumber ?? string.Empty), ct);
    }
}
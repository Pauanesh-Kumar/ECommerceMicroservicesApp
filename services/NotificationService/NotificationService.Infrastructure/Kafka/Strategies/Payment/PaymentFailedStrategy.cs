using System.Globalization;
using MediatR;
using NotificationService.Application.Features.Notifications.Commands.PaymentNotification;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Kafka.Contracts;

namespace NotificationService.Infrastructure.Kafka.Strategies.Payment;

public sealed class PaymentFailedStrategy(IMediator mediator) : IEventStrategy
{
    public NotificationEventType EventType => NotificationEventType.Payment_Failed;

    public Task HandleAsync(object integrationEvent, CancellationToken ct)
    {
        var ev = (PaymentIntegrationEvent)integrationEvent;
        var fullName = $"{ev.FirstName} {ev.LastName}".Trim();
        var amount = ev.Amount.ToString("0.00", CultureInfo.InvariantCulture);

        return mediator.Send(new SendPaymentFailedNotificationCommand(
            ev.UserId,
            ev.Email ?? string.Empty,
            fullName,
            amount,
            ev.PhoneNumber ?? string.Empty), ct);
    }
}
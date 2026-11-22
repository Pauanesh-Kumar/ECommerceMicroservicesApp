using MediatR;
using NotificationService.Domain.Common;

namespace NotificationService.Application.Features.Notifications.Commands.PaymentNotification;

public sealed record SendPaymentProcessedNotificationCommand(
    Guid UserId,
    string Email,
    string FullName,
    string Amount,
    string? PhoneNumber) : IRequest<Result>;

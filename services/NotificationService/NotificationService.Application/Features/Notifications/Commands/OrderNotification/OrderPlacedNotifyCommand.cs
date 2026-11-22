using MediatR;
using NotificationService.Domain.Common;

namespace NotificationService.Application.Features.Notifications.Commands.OrderNotification;
public sealed record SendOrderPlacedNotificationCommand(
    Guid UserId,
    string Email,
    string FullName,
    string OrderId,
    string? PhoneNumber) : IRequest<Result>;

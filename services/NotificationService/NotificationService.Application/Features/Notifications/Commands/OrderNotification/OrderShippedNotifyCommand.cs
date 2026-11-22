using MediatR;
using NotificationService.Domain.Common;

namespace NotificationService.Application.Features.Notifications.Commands.OrderNotification;

public sealed record SendOrderShippedNotificationCommand(
    Guid UserId,
    string Email,
    string FullName,
    string OrderId, 
    string? PhoneNumber) : IRequest<Result>;

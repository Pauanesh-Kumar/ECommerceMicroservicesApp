using MediatR;
using NotificationService.Domain.Common;

namespace NotificationService.Application.Features.Notifications.Commands.UserNotification;

public sealed record SendUserDeactivatedNotificationCommand(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber) : IRequest<Result>;

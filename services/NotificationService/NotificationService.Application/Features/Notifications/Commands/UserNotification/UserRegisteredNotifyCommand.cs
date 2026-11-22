using MediatR;
using NotificationService.Domain.Common;

namespace NotificationService.Application.Features.Notifications.Commands.UserNotification;

/// <summary>Fired when UserService publishes a User_Registered integration event.</summary>
public sealed record SendUserRegisteredNotificationCommand(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber) : IRequest<Result>;

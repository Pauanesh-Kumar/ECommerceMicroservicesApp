using MediatR;
using NotificationService.Domain.Entities;

namespace NotificationService.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed record GetUserNotificationsQuery(Guid UserId) : IRequest<IReadOnlyList<Notification>>;
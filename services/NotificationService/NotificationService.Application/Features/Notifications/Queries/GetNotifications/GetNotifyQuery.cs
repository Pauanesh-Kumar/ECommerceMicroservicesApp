using MediatR;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Queries.GetNotifications;

public sealed record GetNotificationsQuery(int Page = 1, int PageSize = 20)
    : IRequest<IReadOnlyList<Notification>>;


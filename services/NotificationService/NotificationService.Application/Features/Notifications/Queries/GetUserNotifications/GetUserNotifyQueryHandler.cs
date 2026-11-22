using MediatR;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed class GetUserNotificationsQueryHandler(INotificationRepository repo)
    : IRequestHandler<GetUserNotificationsQuery, IReadOnlyList<Notification>>
{
    public Task<IReadOnlyList<Notification>> Handle(GetUserNotificationsQuery request, CancellationToken ct)
        => repo.GetByUserIdAsync(request.UserId, ct);
}

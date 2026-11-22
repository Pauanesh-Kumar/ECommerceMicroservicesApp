using MediatR;
using NotificationService.Application.Features.Notifications.Queries.GetNotifications;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Queries.GetNotifications;

internal sealed class GetNotificationsQueryHandler
    : IRequestHandler<GetNotificationsQuery, IReadOnlyList<Notification>>
{
    private readonly INotificationRepository _repo;
    public GetNotificationsQueryHandler(INotificationRepository repo) => _repo = repo;

    public Task<IReadOnlyList<Notification>> Handle(GetNotificationsQuery request, CancellationToken ct)
        => _repo.GetAllAsync(request.Page, request.PageSize, ct);
}

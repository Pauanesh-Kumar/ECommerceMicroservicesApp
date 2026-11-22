using MediatR;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Queries.GetNotificationsByUser;

internal sealed class GetNotificationsByUserQueryHandler
    : IRequestHandler<GetNotificationsByUserQuery, IReadOnlyList<Notification>>
{
    private readonly INotificationRepository _repo;
    public GetNotificationsByUserQueryHandler(INotificationRepository repo) => _repo = repo;

    public Task<IReadOnlyList<Notification>> Handle(GetNotificationsByUserQuery request, CancellationToken ct)
        => _repo.GetByUserIdAsync(request.UserId, ct);
}
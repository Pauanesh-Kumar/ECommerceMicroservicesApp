using MediatR;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Queries.GetNotificationById;

internal sealed class GetNotificationByIdQueryHandler
    : IRequestHandler<GetNotificationByIdQuery, Notification?>
{
    private readonly INotificationRepository _repo;
    public GetNotificationByIdQueryHandler(INotificationRepository repo) => _repo = repo;

    public Task<Notification?> Handle(GetNotificationByIdQuery request, CancellationToken ct)
        => _repo.GetByIdAsync(request.Id, ct);
}
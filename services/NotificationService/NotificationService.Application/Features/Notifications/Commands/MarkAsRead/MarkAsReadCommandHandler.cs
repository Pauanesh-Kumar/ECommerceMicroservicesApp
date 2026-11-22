using MediatR;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Commands.MarkAsRead;

internal sealed class MarkAsReadCommandHandler : IRequestHandler<MarkAsReadCommand, bool>
{
    private readonly INotificationRepository _repo;
    public MarkAsReadCommandHandler(INotificationRepository repo) => _repo = repo;

    public async Task<bool> Handle(MarkAsReadCommand request, CancellationToken ct)
    {
        var notification = await _repo.GetByIdAsync(request.NotificationId, ct);
        if (notification is null) return false;

        notification.MarkSkipped(); // acknowledged — no further delivery needed
        await _repo.SaveChangesAsync(ct);
        return true;
    }
}
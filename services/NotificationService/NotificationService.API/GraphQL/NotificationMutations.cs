using ECommerce.Shared.Security;
using HotChocolate.Authorization;
using HotChocolate.Types;
using MediatR;
using NotificationService.Application.Features.Notifications.Commands.MarkAsRead;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Api.GraphQL;

// Define the payload type returned by the mutation
public sealed record MarkAsReadPayload(bool Success, string? ErrorMessage);

[ExtendObjectType(typeof(Mutation))]
public sealed class NotificationMutations
{
    [Authorize(Policy = "User")]
    public async Task<MarkAsReadPayload> MarkAsReadAsync(
        Guid id,
        ICurrentUserAccessor currentUser,
        INotificationRepository repo,
        ISender mediator,
        CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return new MarkAsReadPayload(false, "Unable to resolve current user.");

        var notification = await repo.GetByIdAsync(id, ct);
        if (notification is null)
            return new MarkAsReadPayload(false, "Notification not found.");

        if (notification.UserId != userId)
            return new MarkAsReadPayload(false, "You do not have access to this notification.");

        var success = await mediator.Send(new MarkAsReadCommand(id), ct);
        return new MarkAsReadPayload(success, success ? null : "Failed to update notification.");
    }
}

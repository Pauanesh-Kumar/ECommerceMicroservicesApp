using ECommerce.Shared.Security;
using MediatR;
using HotChocolate.Authorization;
using NotificationService.Application.Features.Notifications.Queries.GetNotificationById;
using NotificationService.Application.Features.Notifications.Queries.GetNotifications;
using NotificationService.Application.Features.Notifications.Queries.GetNotificationsByUser;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Api.GraphQL;

[ExtendObjectType(typeof(Query))]
public sealed class NotificationQueries
{
    // Self-service: current user's notifications
    [Authorize(Policy = "User")]
    public async Task<IEnumerable<NotificationDto>?> GetMyNotificationsAsync(
        ICurrentUserAccessor currentUser, INotificationRepository repo, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return null;
        var notifications = await repo.GetByUserIdAsync(userId, ct);
        return notifications.Select(NotificationDto.FromDomain);
    }

    // Admin: lookup by ID
    [Authorize(Policy = "Admin")]
    public async Task<NotificationDto?> GetNotificationByIdAsync(
        Guid id, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNotificationByIdQuery(id), ct);
        return result is null ? null : NotificationDto.FromDomain(result);
    }

    // Admin: lookup by user
    [Authorize(Policy = "Admin")]
    public async Task<IEnumerable<NotificationDto>> GetNotificationsByUserAsync(
        Guid userId, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNotificationsByUserQuery(userId), ct);
        return result.Select(NotificationDto.FromDomain);
    }

    // Admin: paginated global listing
    [Authorize(Policy = "Admin")]
    public async Task<IEnumerable<NotificationDto>> GetNotificationsAsync(
        int page, int pageSize, ISender mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetNotificationsQuery(page, pageSize), ct);
        return result.Select(NotificationDto.FromDomain);
    }
}

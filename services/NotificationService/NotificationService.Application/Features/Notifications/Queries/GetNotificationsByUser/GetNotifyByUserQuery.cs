using MediatR;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Queries.GetNotificationsByUser;

public sealed record GetNotificationsByUserQuery(Guid UserId)
    : IRequest<IReadOnlyList<Notification>>;



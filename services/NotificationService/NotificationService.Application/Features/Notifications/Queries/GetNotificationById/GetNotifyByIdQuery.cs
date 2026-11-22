using MediatR;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Interfaces;
//using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Features.Notifications.Queries.GetNotificationById;

public sealed record GetNotificationByIdQuery(Guid Id) : IRequest<Notification?>;


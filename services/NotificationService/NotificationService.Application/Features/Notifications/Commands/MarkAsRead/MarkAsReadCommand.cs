using MediatR;
using NotificationService.Domain.Interfaces;
//using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Features.Notifications.Commands.MarkAsRead;

public sealed record MarkAsReadCommand(Guid NotificationId) : IRequest<bool>;



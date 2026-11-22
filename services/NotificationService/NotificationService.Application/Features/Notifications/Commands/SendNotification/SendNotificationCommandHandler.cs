using MediatR;
using NotificationService.Application.Common.Interfaces;
using NotificationService.Application.Common.Models;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Commands.SendNotification;

internal sealed class SendNotificationCommandHandler
    : IRequestHandler<SendNotificationCommand, Guid>
{
    private readonly INotificationRepository _repo;
    private readonly IEmailSender _emailSender;
    private readonly ISmsSender _smsSender;

    public SendNotificationCommandHandler(
        INotificationRepository repo,
        IEmailSender emailSender,
        ISmsSender smsSender)
    {
        _repo = repo;
        _emailSender = emailSender;
        _smsSender = smsSender;
    }

    public async Task<Guid> Handle(SendNotificationCommand cmd, CancellationToken ct)
    {
        var notification = Notification.CreateCustom(
            cmd.UserId, cmd.Recipient, cmd.Subject, cmd.Body, cmd.Channel, cmd.EventType);

        await _repo.AddAsync(notification, ct);

        var result = cmd.Channel switch
        {
            NotificationChannel.Email => await _emailSender.SendAsync(
                new EmailMessage(cmd.Recipient, cmd.Subject, HtmlBody: cmd.Body, PlainTextBody: cmd.Body), ct),

            NotificationChannel.Sms => await _smsSender.SendAsync(
                new SmsMessage(cmd.Recipient, cmd.Body), ct),

            _ => Domain.Common.Result.Failure($"Unsupported channel: {cmd.Channel}")
        };

        if (result.IsFailure)
            notification.MarkFailed(result.Error!);
        else
            notification.MarkSent();

        await _repo.SaveChangesAsync(ct);
        return notification.Id;
    }
}
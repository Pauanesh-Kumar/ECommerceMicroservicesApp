using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Common.Interfaces;
using NotificationService.Application.Common.Models;
using NotificationService.Application.Features.Notifications.Common;
using NotificationService.Application.Features.Notifications.Templates;
using NotificationService.Domain.Common;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Interfaces;

namespace NotificationService.Application.Features.Notifications.Commands.UserNotification;

public sealed class SendUserRegisteredNotificationCommandHandler(
    IEmailSender emailSender,
    ISmsSender smsSender,
    IEmailHtmlFormatter htmlFormatter,
    INotificationRepository repo,
    ILogger<SendUserRegisteredNotificationCommandHandler> logger)
    : IRequestHandler<SendUserRegisteredNotificationCommand, Result>
{
    public async Task<Result> Handle(SendUserRegisteredNotificationCommand request, CancellationToken ct)
    {
        var fullName = $"{request.FirstName} {request.LastName}";
        var data = new NotificationTemplateData(FullName: fullName);

        // ── Email ─────────────────────────────────────────────────────────────
        var (emailSubject, emailBody) = NotificationTemplateRegistry.Resolve(
            NotificationEventType.User_Registered, NotificationChannel.Email, data);

        var emailRecord = Notification.Create(
            request.UserId, request.Email, emailSubject!, emailBody,
            NotificationChannel.Email, NotificationEventType.User_Registered);

        var emailMsg = new EmailMessage(
            To: request.Email,
            Subject: emailSubject!,
            HtmlBody: htmlFormatter.ToHtml(emailBody, data),
            PlainTextBody: emailBody);

        var emailResult = await emailSender.SendAsync(emailMsg, ct);
        if (emailResult.IsSuccess) emailRecord.MarkSent();
        else
        {
            emailRecord.MarkFailed(emailResult.Error!);
            logger.LogWarning("Welcome email failed for UserId={UserId}: {Error}", request.UserId, emailResult.Error);
        }
        await repo.AddAsync(emailRecord, ct);

        // ── SMS — optional, no HTML involved ────────────────────────────────
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var (_, smsBody) = NotificationTemplateRegistry.Resolve(
                NotificationEventType.User_Registered, NotificationChannel.Sms, data);

            var smsRecord = Notification.CreateSms(
                request.UserId, request.PhoneNumber, smsBody, NotificationEventType.User_Registered);

            var smsResult = await smsSender.SendAsync(new SmsMessage(request.PhoneNumber, smsBody), ct);
            if (smsResult.IsSuccess) smsRecord.MarkSent();
            else
            {
                smsRecord.MarkFailed(smsResult.Error!);
                logger.LogWarning("Welcome SMS failed for UserId={UserId}: {Error}", request.UserId, smsResult.Error);
            }
            await repo.AddAsync(smsRecord, ct);
        }

        await repo.SaveChangesAsync(ct);
        return emailResult;
    }
}
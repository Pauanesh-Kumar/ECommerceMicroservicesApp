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

namespace NotificationService.Application.Features.Notifications.Commands.PaymentNotification;

public sealed class SendPaymentFailedNotificationCommandHandler(
    IEmailSender emailSender,
    ISmsSender smsSender,
    IEmailHtmlFormatter htmlFormatter,
    INotificationRepository repo,
    ILogger<SendPaymentFailedNotificationCommandHandler> logger)
    : IRequestHandler<SendPaymentFailedNotificationCommand, Result>
{
    public async Task<Result> Handle(SendPaymentFailedNotificationCommand request, CancellationToken ct)
    {
        var data = new NotificationTemplateData(FullName: request.FullName, Amount: request.Amount);

        var (subject, body) = NotificationTemplateRegistry.Resolve(
            NotificationEventType.Payment_Failed, NotificationChannel.Email, data);

        var record = Notification.Create(
            request.UserId, request.Email, subject!, body,
            NotificationChannel.Email, NotificationEventType.Payment_Failed);

        var result = await emailSender.SendAsync(new EmailMessage(
            To: request.Email,
            Subject: subject!,
            HtmlBody: htmlFormatter.ToHtml(body, data),
            PlainTextBody: body), ct);

        if (result.IsSuccess) record.MarkSent();
        else { record.MarkFailed(result.Error!); logger.LogWarning("PaymentFailed email failed UserId={UserId}: {Error}", request.UserId, result.Error); }
        await repo.AddAsync(record, ct);

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            var (_, smsBody) = NotificationTemplateRegistry.Resolve(
                NotificationEventType.Payment_Failed, NotificationChannel.Sms, data);

            var smsRecord = Notification.CreateSms(
                request.UserId, request.PhoneNumber, smsBody, NotificationEventType.Payment_Failed);

            var smsResult = await smsSender.SendAsync(new SmsMessage(request.PhoneNumber, smsBody), ct);
            if (smsResult.IsSuccess) smsRecord.MarkSent();
            else { smsRecord.MarkFailed(smsResult.Error!); logger.LogWarning("PaymentFailed SMS failed UserId={UserId}: {Error}", request.UserId, smsResult.Error); }
            await repo.AddAsync(smsRecord, ct);
        }

        await repo.SaveChangesAsync(ct);
        return result;
    }
}
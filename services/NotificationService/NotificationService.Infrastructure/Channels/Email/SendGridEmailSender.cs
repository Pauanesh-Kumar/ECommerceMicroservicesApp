using Microsoft.Extensions.Options;
using NotificationService.Application.Common.Interfaces;
using NotificationService.Application.Common.Models;
using NotificationService.Domain.Common;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotificationService.Infrastructure.Channels.Email;

/// <summary>
/// SendGrid implementation of IEmailSender.
/// Receives EmailMessage from the Application layer — no Kafka or domain logic here.
/// </summary>
public sealed class SendGridEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public SendGridEmailSender(IOptions<EmailSettings> options)
        => _settings = options.Value;

    public async Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var client = new SendGridClient(_settings.ApiKey);
        var from = new EmailAddress(_settings.FromEmail, _settings.FromName);
        var to = new EmailAddress(message.To);

        var sgMessage = MailHelper.CreateSingleEmail(
            from,
            to,
            message.Subject,
            plainTextContent: message.PlainTextBody ?? string.Empty,
            htmlContent: message.HtmlBody);

        var response = await client.SendEmailAsync(sgMessage, ct);

        if ((int)response.StatusCode is >= 200 and < 300)
        {
            return Result.Success();
        }

        var body = await response.Body.ReadAsStringAsync(ct);
        return Result.Failure($"SendGrid error {(int)response.StatusCode}: {body}");
    }
}
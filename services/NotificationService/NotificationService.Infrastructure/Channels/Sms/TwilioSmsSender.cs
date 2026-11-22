using Microsoft.Extensions.Options;
using NotificationService.Application.Common.Interfaces;
using NotificationService.Application.Common.Models;
using NotificationService.Domain.Common;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotificationService.Infrastructure.Channels.Sms;

/// <summary>
/// Twilio implementation of ISmsSender.
/// Receives SmsMessage from the Application layer.
/// </summary>
public sealed class TwilioSmsSender : ISmsSender
{
    private readonly TwilioSettings _settings;

    public TwilioSmsSender(IOptions<TwilioSettings> options)
    {
        _settings = options.Value;
        TwilioClient.Init(_settings.AccountSid, _settings.AuthToken);
    }

    public async Task<Result> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        try
        {
            var result = await MessageResource.CreateAsync(
                body: message.Body,
                from: new PhoneNumber(_settings.FromPhoneNumber),
                to: new PhoneNumber(message.To));

            return result.Status == MessageResource.StatusEnum.Failed
                ? Result.Failure(result.ErrorMessage ?? "Twilio reported message failed.")
                : Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Twilio send error: {ex.Message}");
        }
    }
}
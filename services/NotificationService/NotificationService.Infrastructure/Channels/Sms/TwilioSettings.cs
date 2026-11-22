namespace NotificationService.Infrastructure.Channels.Sms;

public sealed class TwilioSettings
{
    public const string SectionName = "TwilioSettings";

    public string AccountSid { get; init; } = default!;
    public string AuthToken { get; init; } = default!;
    public string FromPhoneNumber { get; init; } = default!;
}

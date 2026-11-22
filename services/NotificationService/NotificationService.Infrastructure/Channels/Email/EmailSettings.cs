namespace NotificationService.Infrastructure.Channels.Email;

public sealed class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string ApiKey { get; init; } = default!;
    public string FromEmail { get; init; } = default!;
    public string FromName { get; init; } = default!;
}
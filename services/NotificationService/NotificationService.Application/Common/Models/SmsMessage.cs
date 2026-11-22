namespace NotificationService.Application.Common.Models;

/// <summary>Value object carrying everything an SMS channel adapter needs.</summary>
public sealed record SmsMessage(string To, string Body);

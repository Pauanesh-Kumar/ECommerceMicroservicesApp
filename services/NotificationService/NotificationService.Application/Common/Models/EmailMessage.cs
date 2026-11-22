namespace NotificationService.Application.Common.Models;

/// <summary>Value object carrying everything an email channel adapter needs. No domain logic here.</summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);

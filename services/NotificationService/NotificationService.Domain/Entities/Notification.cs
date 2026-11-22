using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Audit record of every notification attempt.
/// NotificationChannel = HOW it was sent (Email / SMS).
/// NotificationEventType = WHY it was sent (User_Registered, Order_Shipped …).
/// Pure Domain entity — knows nothing about template text or wording;
/// that decision is made by the caller (Application layer) and passed in.
/// All construction goes through factory methods to enforce invariants (no invalid state).
/// </summary>
public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>Email address or phone number that was targeted.</summary>
    public string Recipient { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>HOW the notification was delivered (Email, SMS).</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>WHY the notification was triggered (User_Registered, Order_Shipped …).</summary>
    public NotificationEventType EventType { get; private set; }

    public NotificationStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? SentAtUtc { get; private set; }

    private Notification() { } // EF Core

    /// <summary>
    /// Core factory. Now public (was private) — Application layer calls this directly
    /// once it has resolved subject/body via NotificationTemplateRegistry.
    /// No behavior change for existing internal callers (CreateSms/CreateCustom below).
    /// </summary>
    public static Notification Create(
        Guid userId, string recipient, string subject, string body,
        NotificationChannel channel, NotificationEventType eventType) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Channel = channel,
            EventType = eventType,
            Status = NotificationStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

    // ── SMS overload (any event type) — pure Domain, no template dependency, unchanged ──

    public static Notification CreateSms(
        Guid userId, string phoneNumber, string body, NotificationEventType eventType) =>
        Create(userId, phoneNumber, "SMS", body, NotificationChannel.Sms, eventType);

    /// <summary>Flexible factory for direct API / command-driven sends — pure Domain, unchanged.</summary>
    public static Notification CreateCustom(
        Guid userId, string recipient, string subject, string body,
        NotificationChannel channel, NotificationEventType eventType) =>
        Create(userId, recipient, subject, body, channel, eventType);

    // ── Reconstitution (repository rehydration only) — unchanged ──────────────

    /// <summary>Rebuilds a Notification aggregate from a persistence document. Repository use only.</summary>
    public static Notification Reconstitute(
        Guid id, Guid userId, string recipient, string subject, string body,
        NotificationChannel channel, NotificationEventType eventType,
        NotificationStatus status, string? errorMessage,
        DateTime createdAtUtc, DateTime? sentAtUtc) => new()
        {
            Id = id,
            UserId = userId,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Channel = channel,
            EventType = eventType,
            Status = status,
            ErrorMessage = errorMessage,
            CreatedAtUtc = createdAtUtc,
            SentAtUtc = sentAtUtc
        };

    // ── State transitions — unchanged ──────────────────────────────────────────

    public void MarkSent()
    {
        Status = NotificationStatus.Sent;
        SentAtUtc = DateTime.UtcNow;
    }

    public void MarkFailed(string error)
    {
        Status = NotificationStatus.Failed;
        ErrorMessage = error;
    }

    public void MarkSkipped()
        => Status = NotificationStatus.Skipped;
}
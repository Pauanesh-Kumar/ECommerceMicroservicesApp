using NotificationService.Application.Features.Notifications.Common;

namespace NotificationService.Application.Common.Interfaces;

/// <summary>
/// Wraps the registry's plain-text body in HTML for email delivery, preserving
/// emphasis on dynamic values (order IDs, amounts) without duplicating copy.
/// Kept as its own interface (SRP) so the registry stays plain-text-only and
/// channel-agnostic — HTML is a delivery-channel concern, not a content concern.
/// </summary>
public interface IEmailHtmlFormatter
{
    string ToHtml(string plainTextBody, NotificationTemplateData data);
}
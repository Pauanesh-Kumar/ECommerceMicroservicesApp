using NotificationService.Application.Common.Interfaces;
using NotificationService.Application.Features.Notifications.Common;
using System.Net;

namespace NotificationService.Application.Common.Services;

/// <summary>
/// Bolds the same dynamic tokens (order id, amount) your original hand-typed
/// HTML bolded — but derived from the one resolved body, not retyped per handler.
/// Pure text transform, no external dependency — safe to live in Application.
/// </summary>
public sealed class EmailHtmlFormatter : IEmailHtmlFormatter
{
    public string ToHtml(string plainTextBody, NotificationTemplateData data)
    {
        var html = WebUtility.HtmlEncode(plainTextBody);

        if (!string.IsNullOrWhiteSpace(data.OrderId))
            html = html.Replace($"#{data.OrderId}", $"<b>#{data.OrderId}</b>");

        if (!string.IsNullOrWhiteSpace(data.Amount))
            html = html.Replace(data.Amount, $"<b>{data.Amount}</b>");

        return $"<p>{html}</p>";
    }
}
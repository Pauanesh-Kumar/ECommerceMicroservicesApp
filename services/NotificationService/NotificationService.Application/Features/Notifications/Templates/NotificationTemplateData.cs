using System;
using System.Collections.Generic;
using System.Text;

namespace NotificationService.Application.Features.Notifications.Common
{
    /// <summary>Loosely-typed template payload — only the fields a given template needs are set.</summary>
    public sealed record NotificationTemplateData(
        string? FullName = null,
        string? OrderId = null,
        string? Amount = null,
        string? ProductName = null);
}

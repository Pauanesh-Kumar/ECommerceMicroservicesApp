using NotificationService.Application.Common.Models;
using NotificationService.Domain.Common;

namespace NotificationService.Application.Common.Interfaces;

/// <summary>
/// Delivery port for the Email channel.
/// Application defines the contract; Infrastructure provides the adapter (DIP).
/// </summary>
public interface IEmailSender
{
    Task<Result> SendAsync(EmailMessage message, CancellationToken ct = default);
}


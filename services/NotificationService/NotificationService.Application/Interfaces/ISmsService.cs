namespace NotificationService.Application.Interfaces;

public interface ISmsService
{
    Task SendAsync(string to, string body, CancellationToken ct = default);
}

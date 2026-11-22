using MediatR;
using Microsoft.Extensions.Logging;

namespace NotificationService.Application.Behaviors;

/// <summary>MediatR pipeline behavior — logs every command/query entry and exit (SRP: logging only).</summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("[Notification] → {Request}", name);
        var response = await next();
        logger.LogInformation("[Notification] ← {Request}", name);
        return response;
    }
}

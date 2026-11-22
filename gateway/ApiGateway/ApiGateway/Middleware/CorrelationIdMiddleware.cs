namespace ApiGateway.Middleware;

/// <summary>
/// Injects X-Correlation-ID on every request.
/// The same header is forwarded by YARP (configured in appsettings ReverseProxy transforms)
/// so UserService and NotificationService logs carry the same correlation ID.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(Header, out var id)
            || string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString();
            context.Request.Headers[Header] = id;
        }

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", id.ToString()))
        {
            await next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
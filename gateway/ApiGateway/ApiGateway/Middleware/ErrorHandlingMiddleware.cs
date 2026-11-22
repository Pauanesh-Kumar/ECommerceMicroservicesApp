using System.Net.Mime;
using System.Text.Json;

namespace ApiGateway.Middleware;

/// <summary>
/// Catches unhandled exceptions and returns a consistent ProblemDetails JSON payload.
/// Placed before all other middleware so it catches pipeline faults too.
/// </summary>
public sealed class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception — {Method} {Path}",
                context.Request.Method, context.Request.Path);

            // Preserve downstream HTTP status codes (BadGateway, ServiceUnavailable, etc.)
            context.Response.StatusCode = ex is HttpRequestException httpEx
                ? (int)(httpEx.StatusCode ?? System.Net.HttpStatusCode.BadGateway)
                : StatusCodes.Status500InternalServerError;

            context.Response.ContentType = MediaTypeNames.Application.Json;

            var body = JsonSerializer.Serialize(new
            {
                title = "An error occurred processing your request.",
                status = context.Response.StatusCode,
                detail = ex.Message,
                traceId = context.TraceIdentifier
            }, JsonOpts);

            await context.Response.WriteAsync(body);
        }
    }
}

public static class ErrorHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGatewayErrorHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ErrorHandlingMiddleware>();
}
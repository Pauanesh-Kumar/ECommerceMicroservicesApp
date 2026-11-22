using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace UserService.API.Extensions;

/// <summary>
/// Global exception middleware — converts all unhandled exceptions to
/// RFC 7807 ProblemDetails JSON responses.
///
/// SOLID — Single Responsibility: only exception-to-HTTP translation here.
/// All business errors return via Result pattern before reaching this middleware.
/// This catches only truly unexpected errors (bugs, infra outages).
/// </summary>
public sealed class GlobalExceptionMiddleware(
    RequestDelegate                    next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment                   env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            // FluentValidation errors from pipeline behaviors
            logger.LogWarning("Validation failed: {Errors}", ex.Message);
            await WriteProblemAsync(context, StatusCodes.Status422UnprocessableEntity,
                "Validation Failed",
                "One or more validation errors occurred.",
                new { errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage }) });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            var detail = env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.";

            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "Internal Server Error", detail, null);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context, int status, string title, string detail, object? extensions)
    {
        context.Response.StatusCode  = status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Type     = $"https://httpstatuses.com/{status}",
            Title    = title,
            Status   = status,
            Detail   = detail,
            Instance = context.Request.Path
        };

        problem.Extensions["traceId"]       = context.TraceIdentifier;
        problem.Extensions["correlationId"] = context.Items["CorrelationId"]?.ToString() ?? "";

        if (extensions is not null)
            problem.Extensions["validationErrors"] = extensions;

        await context.Response.WriteAsJsonAsync(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

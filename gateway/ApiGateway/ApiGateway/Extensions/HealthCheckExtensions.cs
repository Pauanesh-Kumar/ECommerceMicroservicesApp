using ApiGateway.Configuration;

namespace ApiGateway.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>
    /// Probes /health/live on each downstream service.
    /// Matches the health endpoints mapped in NotificationService (MapHealthChecks("/health/live"))
    /// and UserService (MapHealthChecks("/health")).
    /// </summary>
    public static IServiceCollection AddGatewayHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var opts = configuration
            .GetSection(ServicesOptions.Section)
            .Get<ServicesOptions>() ?? new ServicesOptions();

        services.AddHealthChecks()
            .AddUrlGroup(
                new Uri($"{opts.UserServiceBaseUrl}/health"),
                name: "user-service",
                tags: ["downstream", "user"])
            .AddUrlGroup(
                new Uri($"{opts.NotificationServiceBaseUrl}/health/live"),
                name: "notification-service",
                tags: ["downstream", "notification"]);

        return services;
    }
}
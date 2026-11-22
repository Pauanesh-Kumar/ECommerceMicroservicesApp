using ApiGateway.Configuration;

namespace ApiGateway.Extensions;

public static class HttpClientExtensions
{
    /// <summary>
    /// Named HttpClients used ONLY by aggregator endpoints.
    /// YARP handles all plain proxy routes — these clients are for custom orchestration only.
    /// AddStandardResilienceHandler adds Polly retry + circuit-breaker automatically.
    /// </summary>
    public static IServiceCollection AddDownstreamClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var opts = configuration
            .GetSection(ServicesOptions.Section)
            .Get<ServicesOptions>()
            ?? throw new InvalidOperationException("Services configuration section is missing.");

        services.Configure<ServicesOptions>(configuration.GetSection(ServicesOptions.Section));

        services.AddHttpClient("UserService", c =>
        {
            c.BaseAddress = new Uri(opts.UserServiceBaseUrl);
            c.DefaultRequestHeaders.Add("Accept", "application/json");
            c.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddStandardResilienceHandler();

        services.AddHttpClient("NotificationService", c =>
        {
            c.BaseAddress = new Uri(opts.NotificationServiceBaseUrl);
            c.DefaultRequestHeaders.Add("Accept", "application/json");
            c.Timeout = TimeSpan.FromSeconds(10);
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
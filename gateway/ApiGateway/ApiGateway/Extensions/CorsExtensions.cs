namespace ApiGateway.Extensions;

public static class CorsExtensions
{
    public const string PolicyName = "GatewayPolicy";

    public static IServiceCollection AddGatewayCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var origins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        services.AddCors(opt => opt.AddPolicy(PolicyName, p =>
        {
            // Fail closed: only explicitly configured origins (the Angular app) are allowed; "*" is rejected.
            p.WithOrigins(origins.Where(o => o != "*").ToArray()).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        }));

        return services;
    }
}
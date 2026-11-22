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
            .Get<string[]>() ?? ["*"];

        services.AddCors(opt => opt.AddPolicy(PolicyName, p =>
        {
            if (origins is ["*"])
                p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
            else
                p.WithOrigins(origins).AllowAnyMethod().AllowAnyHeader().AllowCredentials();
        }));

        return services;
    }
}
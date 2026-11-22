using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UserService.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddDatabase(configuration)
            .AddRedisCache(configuration)
            .AddKafkaProducer(configuration)
            .AddSecurityServices();
            

        // Simple startup migration: register hosted service that runs ApplyMigrationsAsync.
        // Keep migrations configurable via Database:MigrateOnStartup (default true/false in settings).
        services.AddHostedService<MigrationsHostedService>();
        return services;
    }
}
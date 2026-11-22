using CartService.Application.Interfaces;
using CartService.Domain.Repositories;
using CartService.Infrastructure.Clients;
using CartService.Infrastructure.Persistence;
using CartService.Infrastructure.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace CartService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddCartInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Reuses the shared Redis container — cart keys are prefixed "cart:*" so they
        // never collide with UserService's "user:*" cache keys on the same instance.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration["Redis:ConnectionString"]!));
        services.Configure<CartOptions>(configuration.GetSection(CartOptions.Section));
        services.AddScoped<IRedisCartRepository, RedisCartRepository>();

        // AddStandardResilienceHandler = retry + circuit breaker + per-attempt/total
        // timeouts in one Polly pipeline — same package + technique as
        // ApiGateway.HttpClientExtensions already uses for its downstream clients,
        // tuned here for an internal same-cluster dependency (see
        // OrderServiceResilienceOptions for the full rationale per setting).
        services.AddHttpClient("OrderService", c =>
                c.BaseAddress = new Uri(configuration["Services:OrderServiceBaseUrl"]!))
            .AddStandardResilienceHandler(OrderServiceResilienceOptions.Configure);

        services.AddScoped<IOrderServiceClient, OrderServiceClient>();

        return services;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System;
using UserService.Application.Interfaces;
using UserService.Infrastructure.Cache;

namespace UserService.Infrastructure.Extensions;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException(
                "Redis:ConnectionString not configured. Set Redis__ConnectionString env var.");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(conn));
        services.Configure<CacheOptions>(configuration.GetSection(CacheOptions.Section));
        services.AddScoped<IUserCacheService, UserCacheService>();
        return services;
    }
}

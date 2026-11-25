using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Kafka;
using AnalyticsService.Infrastructure.Persistence;
using AnalyticsService.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace AnalyticsService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(configuration.GetConnectionString("AnalyticsServiceDb")));
        services.AddSingleton<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("analyticsservice_db"));

        services.AddScoped<IAnalyticsRepository, AnalyticsRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();
        services.Configure<AnalyticsKafkaOptions>(configuration.GetSection(AnalyticsKafkaOptions.Section));

        services.AddHostedService<AllTopicsConsumerService>();
        services.AddHostedService<ReportingProjectionHostedService>();

        return services;
    }
}

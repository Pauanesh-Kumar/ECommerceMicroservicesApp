using Confluent.Kafka;
using ECommerce.Shared.Infrastructure.kafka;
using ECommerce.Shared.Infrastructure.Redis;
using ECommerce.Shared.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

namespace ECommerce.Shared.Infrastructure.Extensions;

public static class SharedInfrastructureExtensions
{
    public static IServiceCollection AddKafkaProducer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var kafkaConfig = new ProducerConfig();
        configuration.GetSection("Kafka:Producer").Bind(kafkaConfig);

        kafkaConfig.BootstrapServers ??= configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");

        services.AddSingleton(kafkaConfig);
        services.AddSingleton<IKafkaProducer, KafkaProducerWrapper>();
        return services;
    }

    /// <summary>
    /// Registers a TRANSIENT consumer: every hosted service gets its own instance
    /// because IConsumer is not thread-safe. Pass a unique groupId per hosted service.
    /// </summary>
    public static IServiceCollection AddKafkaConsumer(
        this IServiceCollection services,
        IConfiguration configuration,
        string? groupId = null)
    {
        var template = new ConsumerConfig();
        configuration.GetSection("Kafka:Consumer").Bind(template);

        template.BootstrapServers ??= configuration["Kafka:BootstrapServers"]
            ?? throw new InvalidOperationException("Kafka:BootstrapServers not configured");
        template.GroupId = groupId ?? template.GroupId
            ?? throw new InvalidOperationException("Kafka consumer GroupId not configured");

        services.AddTransient<IKafkaConsumer>(sp => new KafkaConsumerWrapper(
            new ConsumerConfig(template),                    // copy, never share config state
            sp.GetRequiredService<ILogger<KafkaConsumerWrapper>>()));

        return services;
    }

    public static IServiceCollection AddSharedRedis(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnection = configuration["Redis:Connection"] ?? configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:Connection or Redis:ConnectionString must be configured");

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddScoped<IIdempotencyStore, RedisIdempotencyStore>();
        return services;
    }

    public static IServiceCollection AddOpenTelemetryTracing(
    this IServiceCollection services,
    IConfiguration configuration,
    string serviceName)
    {
        var otlpEndpoint = configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317";

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName))
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);
                    });
            });

        return services;
    }

    public static IServiceCollection AddStructuredLogging(
        this IServiceCollection services,
        string logLevel = "Information",
        string? logPath = null)
    {
        var level = Enum.TryParse<LogEventLevel>(logLevel, out var parsedLevel)
            ? parsedLevel
            : LogEventLevel.Information;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production")
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");

        if (!string.IsNullOrEmpty(logPath))
        {
            loggerConfig.WriteTo.File(
                path: logPath,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30);
        }

        Log.Logger = loggerConfig.CreateLogger();
        services.AddSerilog(Log.Logger);
        return services;
    }

    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
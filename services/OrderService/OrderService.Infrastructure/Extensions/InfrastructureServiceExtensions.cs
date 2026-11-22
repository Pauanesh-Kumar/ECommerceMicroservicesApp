using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Interfaces;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Kafka;
using OrderService.Infrastructure.Kafka.Strategies;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddOrderInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrderDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("OrderServiceDb"), npg =>
                npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderUnitOfWork, EfOrderUnitOfWork>();

        services.Configure<OrderKafkaOptions>(configuration.GetSection(OrderKafkaOptions.Section));
        var bootstrap = configuration["Kafka:BootstrapServers"];
        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrap, Acks = Acks.All, EnableIdempotence = true }).Build());

        services.AddScoped<IOrderEventStrategy, StockReservedStrategy>();
        services.AddScoped<IOrderEventStrategy, StockReservationFailedStrategy>();
        services.AddScoped<IOrderEventStrategy, PaymentProcessedStrategy>();
        services.AddScoped<IOrderEventStrategy, PaymentFailedStrategy>();
        services.AddScoped<OrderEventStrategyResolver>();

        services.AddHostedService<SagaEventConsumerService>();
        services.AddHostedService<OutboxPublisherHostedService>();

        return services;
    }

    public static async Task ApplyOrderMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Database.MigrateAsync();
    }
}

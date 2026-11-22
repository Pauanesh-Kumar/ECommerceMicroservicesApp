using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Kafka;
using ProductService.Infrastructure.Persistence;

namespace ProductService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddProductInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMongoClient>(_ => new MongoClient(configuration.GetConnectionString("ProductServiceDb")));
        services.AddSingleton<IMongoDatabase>(sp => sp.GetRequiredService<IMongoClient>().GetDatabase("productservice_db"));

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddSingleton<InboxStore>();
        services.AddHostedService<ProductIndexInitializer>(); // wires the index/inbox setup at startup — see file comment

        services.Configure<ProductKafkaOptions>(configuration.GetSection(ProductKafkaOptions.Section));
        var bootstrap = configuration["Kafka:BootstrapServers"];
        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(new ProducerConfig
            {
                BootstrapServers = bootstrap,
                Acks = Acks.All,
                EnableIdempotence = true
            }).Build());

        services.AddSingleton<IProductEventPublisher, ProductEventPublisher>();
        services.AddHostedService<OrderEventConsumerService>();

        return services;
    }
}

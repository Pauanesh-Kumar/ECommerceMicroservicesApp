using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;
using ProductService.Infrastructure.Kafka;
using ProductService.Infrastructure.Persistence;

namespace ProductService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddProductInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(Product)))
        {
            BsonClassMap.RegisterClassMap<Product>(map =>
            {
                map.AutoMap();
                map.MapIdMember(product => product.Id).SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
            });
        }

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

        services.AddScoped<TransactionalInventoryProcessor>();
        services.AddHostedService<OrderEventConsumerService>();
        services.AddHostedService<InventoryOutboxHostedService>();

        return services;
    }
}

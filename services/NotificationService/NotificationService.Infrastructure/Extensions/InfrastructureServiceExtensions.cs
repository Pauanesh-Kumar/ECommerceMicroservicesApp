using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using NotificationService.Application.Common.Interfaces;
using NotificationService.Domain.Interfaces;
using NotificationService.Infrastructure.Channels.Email;
using NotificationService.Infrastructure.Channels.Sms;
using NotificationService.Infrastructure.Kafka.Consumers;
using NotificationService.Infrastructure.Kafka.Settings;
using NotificationService.Infrastructure.Kafka.Strategies;
using NotificationService.Infrastructure.Kafka.Strategies.Order;
using NotificationService.Infrastructure.Kafka.Strategies.Payment;
using NotificationService.Infrastructure.Kafka.Strategies.User;
using NotificationService.Infrastructure.Persistence.MongoDb;
using NotificationService.Infrastructure.Persistence.Repositories;

namespace NotificationService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MongoDbSettings>(
            configuration.GetSection(MongoDbSettings.SectionName));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var settings = configuration
                .GetSection(MongoDbSettings.SectionName)
                .Get<MongoDbSettings>() ?? new MongoDbSettings();

            return new MongoClient(settings.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var settings = configuration
                .GetSection(MongoDbSettings.SectionName)
                .Get<MongoDbSettings>() ?? new MongoDbSettings();

            return client.GetDatabase(settings.DatabaseName);
        });

        services.AddHostedService<MongoDbInitializer>();

        services.AddScoped<INotificationRepository, NotificationRepository>();

        services.Configure<EmailSettings>(
            configuration.GetSection(EmailSettings.SectionName));
        services.AddScoped<IEmailSender, SendGridEmailSender>();

        services.Configure<TwilioSettings>(
            configuration.GetSection(TwilioSettings.SectionName));
        services.AddScoped<ISmsSender, TwilioSmsSender>();

        services.Configure<KafkaSettings>(
            configuration.GetSection(KafkaSettings.SectionName));

        services.AddScoped<IEventStrategy, UserRegisteredStrategy>();
        services.AddScoped<IEventStrategy, UserUpdatedStrategy>();
        services.AddScoped<IEventStrategy, UserDeactivatedStrategy>();
        services.AddScoped<IEventStrategy, UserPasswordChangedStrategy>();
        services.AddScoped<IEventStrategy, OrderPlacedStrategy>();
        services.AddScoped<IEventStrategy, OrderShippedStrategy>();
        services.AddScoped<IEventStrategy, PaymentProcessedStrategy>();
        services.AddScoped<IEventStrategy, PaymentFailedStrategy>();
        services.AddScoped<EventStrategyResolver>();

        services.AddHostedService<UserEventConsumerService>();
        services.AddHostedService<OrderEventConsumerService>();
        services.AddHostedService<PaymentEventConsumerService>();

        return services;
    }
}
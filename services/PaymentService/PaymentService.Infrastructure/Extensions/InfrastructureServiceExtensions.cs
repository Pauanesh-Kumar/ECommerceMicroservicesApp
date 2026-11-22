using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentService.Application.Interfaces;
using PaymentService.Domain.Repositories;
using PaymentService.Infrastructure.Gateways;
using PaymentService.Infrastructure.Kafka;
using PaymentService.Infrastructure.Persistence;
using PaymentService.Infrastructure.Resilience;

namespace PaymentService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddPaymentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PaymentDbContext>(opt =>
            opt.UseNpgsql(configuration.GetConnectionString("PaymentServiceDb"), npg =>
                npg.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IPaymentUnitOfWork, EfPaymentUnitOfWork>();

        services.Configure<RazorpaySettings>(configuration.GetSection(RazorpaySettings.SectionName));

        // Note: RazorpayPaymentGateway sets DefaultRequestHeaders.Authorization and
        // BaseAddress itself from IOptions<RazorpaySettings> in its constructor — the
        // resilience handler wraps around that same typed client, so the Basic-auth
        // header is already attached before any retry/circuit-breaker logic runs.
        services.AddHttpClient<IPaymentGateway, RazorpayPaymentGateway>()
            .AddStandardResilienceHandler(RazorpayResilienceOptions.Configure);

        services.Configure<PaymentKafkaOptions>(configuration.GetSection(PaymentKafkaOptions.Section));
        var bootstrap = configuration["Kafka:BootstrapServers"];
        services.AddSingleton<IProducer<string, string>>(_ =>
            new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = bootstrap, Acks = Acks.All, EnableIdempotence = true }).Build());

        services.AddHostedService<PaymentRequestedConsumerService>();
        services.AddHostedService<OutboxPublisherHostedService>();

        return services;
    }

    public static async Task ApplyPaymentMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync();
    }
}

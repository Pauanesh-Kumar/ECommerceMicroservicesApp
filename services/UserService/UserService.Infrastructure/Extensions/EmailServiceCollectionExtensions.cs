using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Interfaces;
using UserService.Infrastructure;
using UserService.Infrastructure.Channels;

namespace UserService.Infrastructure.Extensions;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Section));
        services.AddScoped<IEmailService, EmailService>();
        return services;
    }
}
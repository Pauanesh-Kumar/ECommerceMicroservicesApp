using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Behaviors;
using NotificationService.Application.Common.Interfaces;
using NotificationService.Application.Common.Services;
using System.Reflection;

namespace NotificationService.Application.Extensions;


public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>)); // consistent pipeline registration
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton<IEmailHtmlFormatter, EmailHtmlFormatter>();
        return services;
    }
}


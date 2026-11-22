using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Shared.Security;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCurrentUserAccessor(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
        return services;
    }
}

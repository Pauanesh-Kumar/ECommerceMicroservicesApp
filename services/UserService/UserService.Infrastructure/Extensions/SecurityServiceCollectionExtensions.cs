using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Interfaces;
using UserService.Infrastructure.Security;

namespace UserService.Infrastructure.Extensions;

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Named HttpClients
        services.AddHttpClient("google");
        services.AddHttpClient("facebook");
        services.AddHttpClient("twitter");

        // Providers (ensure classes implement IExternalOAuthProvider)
        services.AddSingleton<IExternalOAuthProvider, GoogleOAuthProvider>();
        services.AddSingleton<IExternalOAuthProvider, FacebookOAuthProvider>();
        services.AddSingleton<IExternalOAuthProvider, TwitterOAuthProvider>();

        // Strategy implementing application-facing interface
        services.AddSingleton<IExternalOAuthService, ExternalOAuthService>();

        return services;
    }
}
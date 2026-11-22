using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Infrastructure.Persistence;
using UserService.Infrastructure.Repositories;
using UserService.Domain.Repositories;
using System;

namespace UserService.Infrastructure.Extensions;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var dbOpts = configuration
            .GetSection(DatabaseOptions.Section)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        var conn = configuration.GetConnectionString("UserServiceDb")
            ?? throw new InvalidOperationException(
                "Connection string 'UserServiceDb' not configured.");

        services.AddDbContext<UserDbContext>(opt =>
            opt.UseNpgsql(conn, npg =>
            {
                npg.EnableRetryOnFailure(dbOpts.MaxRetryCount,
                    TimeSpan.Parse(dbOpts.MaxRetryDelay), null);
                npg.CommandTimeout(dbOpts.CommandTimeout);
                npg.MigrationsAssembly(typeof(UserDbContext).Assembly.FullName);
            })
                .EnableSensitiveDataLogging(dbOpts.EnableSensitiveDataLogging)
                .EnableDetailedErrors(dbOpts.EnableDetailedErrors));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        return services;
    }
}
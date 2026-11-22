
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using UserService.Application.Interfaces;
using UserService.Domain.Repositories;
using UserService.Infrastructure.Cache;
using UserService.Infrastructure.Channels;
using UserService.Infrastructure.Kafka;
using UserService.Infrastructure.Persistence;
using UserService.Infrastructure.Repositories;
using UserService.Infrastructure.Security;


namespace UserService.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Registers EF Core + UserDbContext using the connection string that comes from:
    ///
    ///   Priority 1 (highest): Environment variable  ConnectionStrings__UserServiceDb
    ///   Priority 2           : appsettings.{Environment}.json → ConnectionStrings:UserServiceDb
    ///   Priority 3 (lowest)  : appsettings.json              → ConnectionStrings:UserServiceDb
    ///
    /// ASP.NET Core Configuration merges these sources automatically in the order above,
    /// so docker-compose / Kubernetes env vars always win without touching any code.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. Bind strongly-typed database options ───────────────────────────
        var dbOptions = configuration
            .GetSection(DatabaseOptions.Section)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        // ── 2. Read connection string ─────────────────────────────────────────
        //  The key "UserServiceDb" matches both:
        //    - appsettings.json  →  "ConnectionStrings": { "UserServiceDb": "..." }
        //    - Env var           →  ConnectionStrings__UserServiceDb=...
        var connectionString = configuration.GetConnectionString("UserServiceDb");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'UserServiceDb' is not configured. " +
                "Set it via appsettings.Development.json or the environment variable " +
                "ConnectionStrings__UserServiceDb.");

        // ── 3. Register DbContext with production-grade Npgsql options ────────
        services.AddDbContext<UserDbContext>(options =>
        {
            options
                .UseNpgsql(connectionString, npgsql =>
                {
                    // Retry on transient failures (network blips, postgres restart)
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: dbOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.Parse(dbOptions.MaxRetryDelay),
                        errorCodesToAdd: null);

                    // Timeout for individual SQL commands
                    npgsql.CommandTimeout(dbOptions.CommandTimeout);

                    // Tells EF which assembly contains Migrations
                    npgsql.MigrationsAssembly(typeof(UserDbContext).Assembly.FullName);
                })
                // Log parameter values only in Development
                .EnableSensitiveDataLogging(dbOptions.EnableSensitiveDataLogging)
                // Detailed EF exception messages only in Development
                .EnableDetailedErrors(dbOptions.EnableDetailedErrors)
                // Log EF-generated SQL at Debug level
                .LogTo(Console.WriteLine, LogLevel.Debug);
        });

        // ── 4. Register repository and unit of work ───────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }

    // ── EF Core migration helper ─────────────────────────────────────────────
    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<UserDbContext>>();
        try
        {
            logger.LogInformation("Applying EF Core migrations for UserService...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Migration failed — check the DB connection string.");
            throw;
        }
    }
}





using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used exclusively by EF Core CLI tooling (dotnet ef migrations / database update).
/// Bypasses the full application host (Kafka, Redis, Serilog, OpenTelemetry, etc.) so that migrations
/// can be authored/applied without any of those dependencies needing to be running.
///
/// This is NOT used at runtime — the actual app registers UserDbContext via
/// AddDbContext(...) in Program.cs / DependencyInjection.cs as usual.
/// </summary>
public class UserDbContextFactory : IDesignTimeDbContextFactory<UserDbContext>
{
    public UserDbContext CreateDbContext(string[] args)
    {
        // Resolve appsettings from the API project (startup project), matching
        // the --startup-project passed on the CLI, so the same connection string
        // used at runtime is used here too.
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "UserService.API");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.Exists(basePath) ? basePath : Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("UserServiceDb")
            ?? configuration["ConnectionStrings:UserServiceDb"]
            ?? throw new InvalidOperationException(
                "Connection string 'UserServiceDb' not found. Ensure appsettings.json " +
                "(or ConnectionStrings__UserServiceDb env var) is available at design time.");

        var optionsBuilder = new DbContextOptionsBuilder<UserDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
            npgsqlOptions.MigrationsAssembly(typeof(UserDbContextFactory).Assembly.FullName));

        return new UserDbContext(optionsBuilder.Options);
    }
}

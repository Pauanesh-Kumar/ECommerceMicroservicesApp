using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using UserService.Infrastructure.Persistence;

namespace UserService.Infrastructure.Extensions;

internal sealed class MigrationsHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MigrationsHostedService> _logger;
    private readonly IConfiguration _configuration;

    public MigrationsHostedService(
        IServiceProvider services,
        ILogger<MigrationsHostedService> logger,
        IConfiguration configuration)
    {
        _services = services;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dbOpts = _configuration
                .GetSection(DatabaseOptions.Section)
                .Get<DatabaseOptions>() ?? new DatabaseOptions();

            if (!dbOpts.MigrateOnStartup)
            {
                _logger.LogInformation("Database migrations disabled (MigrateOnStartup=false).");
                return;
            }

            _logger.LogInformation("Applying EF Core migrations (hosted service)...");
            // ApplyMigrationsAsync is the extension on IServiceProvider in MigrationExtensions.cs
            await _services.ApplyMigrationsAsync();
            _logger.LogInformation("EF Core migrations completed.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "MigrationsHostedService failed while applying migrations.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

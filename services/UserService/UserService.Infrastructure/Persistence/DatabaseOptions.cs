namespace UserService.Infrastructure.Persistence;

/// <summary>
/// Strongly-typed binding of the "Database" section in appsettings.
/// Avoids magic strings when configuring EF Core options.
/// </summary>
public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public int    MaxRetryCount              { get; init; } = 3;
    public string MaxRetryDelay             { get; init; } = "00:00:05";
    public int    CommandTimeout             { get; init; } = 30;
    public bool   EnableSensitiveDataLogging { get; init; } = false;
    public bool   EnableDetailedErrors       { get; init; } = false;
    public bool   MigrateOnStartup           { get; init; } = false;
}

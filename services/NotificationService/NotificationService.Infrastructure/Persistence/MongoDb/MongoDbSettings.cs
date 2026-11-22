namespace NotificationService.Infrastructure.Persistence.MongoDb;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; init; } = "mongodb://localhost:27019";
    public string DatabaseName { get; init; } = "NotificationDb";
    public string CollectionName { get; init; } = "notifications";
}

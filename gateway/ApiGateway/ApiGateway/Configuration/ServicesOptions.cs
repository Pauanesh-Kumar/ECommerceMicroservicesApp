namespace ApiGateway.Configuration;

/// <summary>
/// Downstream service base URLs.
///
/// Local (non-Docker) dev defaults match launchSettings.json Kestrel ports:
///   UserService: http://localhost:5042
///   NotificationService: http://localhost:5126
///
/// When running under docker-compose, these are overridden via env vars
/// (docker/.env → env_file) using Docker DNS service names + container ports:
///   Services__UserServiceBaseUrl=http://userservice-api:8080
///   Services__NotificationServiceBaseUrl=http://notification-api:8082
///
/// IMPORTANT: "localhost" inside a container refers to the container itself,
/// never another container — always use the Docker Compose service name.
/// </summary>
public sealed class ServicesOptions
{
    public const string Section = "Services";

    public string UserServiceBaseUrl { get; init; } = "http://localhost:5042";
    public string NotificationServiceBaseUrl { get; init; } = "http://localhost:5126";
}
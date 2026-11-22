namespace ApiGateway.Configuration;

/// <summary>
/// Strongly-typed JWT settings — mirrors Jwt section in appsettings.json.
/// Same key/issuer/audience as UserService so the Gateway can validate tokens
/// without issuing them.
/// </summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = "ecommerce-platform";
    public string Audience { get; init; } = "ecommerce-api";
}
namespace ECommerce.Shared.Security;
/// <summary>
/// Abstracts "who is calling" away from transport. Every service that needs the
/// current authenticated user's id depends on this instead of parsing
/// ClaimsPrincipal independently — one implementation, shared as a stable
/// infrastructure library (Shared Kernel), not duplicated per service.
/// </summary>
public interface ICurrentUserAccessor
{
    Guid? UserId { get; }
}

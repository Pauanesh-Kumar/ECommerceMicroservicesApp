namespace UserService.Application.Interfaces;

/// <summary>
/// SOLID — Interface Segregation Principle:
/// Each interface below covers exactly ONE capability.
/// Application layer depends on abstractions, never on concrete implementations.
/// Infrastructure layer provides the concrete classes.
///
/// Every event carries PhoneNumber (nullable) for consistency — so any downstream
/// consumer (e.g. NotificationService's SMS channel) can rely on the field being
/// present on every user event rather than only some of them.
/// </summary>

/// ── Integration event publishing ──────────────────────────────────────────────
public interface IUserEventPublisher
{
    Task PublishUserRegisteredAsync(Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);
    Task PublishUserUpdatedAsync(Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);
    Task PublishUserDeactivatedAsync(Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);
    Task PublishUserPasswordChangedAsync(Guid userId, string email, string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);
}

namespace UserService.Domain.Entities;

public sealed class User
{
    private User() { } // Required by EF Core

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? PhoneNumber { get; private set; }
    public UserStatus Status { get; private set; }
    public Guid RoleId { get; private set; }              // NEW
    public Role Role { get; private set; } = null!;        // NEW
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public static User Create(
        string email,
        string passwordHash,
        string firstName,
        string lastName,
        string? phoneNumber = null,
        Guid? roleId = null)                                // NEW
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email is required.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("Password hash is required.");
        if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Last name is required.");

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            Status = UserStatus.Active,
            RoleId = roleId ?? Role.UserId,             // NEW
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string firstName, string lastName, string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(firstName)) throw new ArgumentException("First name is required.");
        if (string.IsNullOrWhiteSpace(lastName)) throw new ArgumentException("Last name is required.");

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        PhoneNumber = phoneNumber?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Admin-only. Kept separate from UpdateProfile by design.</summary>
    public void AssignRole(Guid roleId, Role role)
    {
        RoleId = roleId;
        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (Status == UserStatus.Inactive)
            throw new InvalidOperationException("User is already inactive.");
        Status = UserStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        Status = UserStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new ArgumentException("Password hash is required.");
        PasswordHash = newPasswordHash;
        UpdatedAt = DateTime.UtcNow;
    }
}

public enum UserStatus { Active, Inactive, Suspended, PendingVerification }
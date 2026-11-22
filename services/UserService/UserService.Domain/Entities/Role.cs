namespace UserService.Domain.Entities;

public sealed class Role
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;   // "Admin" | "User"

    public DateTime CreatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    private Role() { }  // EF

    public Role(Guid id, string name, DateTime createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        IsDeleted = false;
    }

    /// <summary>
    /// Soft-delete only. Roles are never hard-deleted — a User row may still
    /// reference this RoleId (FK is Restrict), so the row must survive for
    /// history/audit purposes even once retired from active use.
    /// </summary>
    public void MarkDeleted(DateTime deletedAt)
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = deletedAt;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
    }

    public static readonly Guid AdminId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid UserId = Guid.Parse("00000000-0000-0000-0000-000000000002");
}
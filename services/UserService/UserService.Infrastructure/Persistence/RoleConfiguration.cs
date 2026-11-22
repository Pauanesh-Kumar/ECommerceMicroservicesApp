using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(50);
        builder.HasIndex(r => r.Name).IsUnique();

        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.DeletedAt);
        builder.Property(r => r.IsDeleted).IsRequired().HasDefaultValue(false);

        builder.HasData(
            new Role(Role.AdminId, "Admin", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            new Role(Role.UserId, "User", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        );
    }
}
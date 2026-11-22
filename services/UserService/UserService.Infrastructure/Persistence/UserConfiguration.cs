using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Persistence;

/// <summary>
/// EF Core entity mapping — kept completely separate from the Domain entity.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "public");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
               .HasColumnName("id")
               .ValueGeneratedNever(); // We generate the Guid in the domain

        builder.Property(u => u.Email)
               .HasColumnName("email")
               .HasMaxLength(255)
               .IsRequired();
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
               .HasColumnName("password_hash")
               .HasMaxLength(500)
               .IsRequired();

        builder.Property(u => u.FirstName)
               .HasColumnName("first_name")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(u => u.LastName)
               .HasColumnName("last_name")
               .HasMaxLength(100)
               .IsRequired();

        builder.Property(u => u.PhoneNumber)
               .HasColumnName("phone_number")
               .HasMaxLength(20);

        builder.Property(u => u.Status)
               .HasColumnName("status")
               .HasConversion<string>()
               .HasMaxLength(30)
               .IsRequired();

        builder.Property(u => u.CreatedAt)
               .HasColumnName("created_at")
               .IsRequired();

        builder.Property(u => u.UpdatedAt)
               .HasColumnName("updated_at")
               .IsRequired();
        
        builder.Property(u => u.RoleId).HasDefaultValue(Role.UserId);

        builder.HasOne(u => u.Role)
               .WithMany()
               .HasForeignKey(u => u.RoleId)
               .OnDelete(DeleteBehavior.Restrict);
    }
}

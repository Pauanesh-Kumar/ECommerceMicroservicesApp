using Microsoft.EntityFrameworkCore;
using PaymentService.Domain.Entities;

namespace PaymentService.Infrastructure.Persistence;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(b =>
        {
            b.ToTable("payments");
            b.HasKey(p => p.Id);
            b.Property(p => p.Amount).HasColumnType("numeric(18,2)");
            b.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
            b.Property(p => p.Currency).HasMaxLength(3);

            // THE idempotency guard: at most one Payment row per Order, enforced at the
            // database level (not just an application-side check-then-insert, which has
            // a race window under concurrent duplicate "payment-requested" deliveries).
            b.HasIndex(p => p.OrderId).IsUnique();
            b.HasIndex(p => p.ProviderOrderId);

            // Keyset-pagination index for the admin ledger listing.
            b.HasIndex(p => new { p.CreatedAtUtc, p.Id }).HasDatabaseName("ix_payments_cursor");
        });

        modelBuilder.Entity<OutboxMessage>(b => { b.ToTable("outbox_messages"); b.HasKey(o => o.Id); b.HasIndex(o => o.ProcessedAtUtc); });
        modelBuilder.Entity<InboxMessage>(b => { b.ToTable("inbox_messages"); b.HasKey(i => i.EventId); });

        base.OnModelCreating(modelBuilder);
    }
}

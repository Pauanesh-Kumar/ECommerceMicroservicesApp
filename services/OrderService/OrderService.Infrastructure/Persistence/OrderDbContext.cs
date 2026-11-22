using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(b =>
        {
            b.ToTable("orders");
            b.HasKey(o => o.Id);
            b.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
            b.Property(o => o.TotalAmount).HasColumnType("numeric(18,2)");
            b.Property(o => o.Currency).HasMaxLength(3);

            // Map Postgres' built-in xmin system column as the EF concurrency token.
            b.Property(o => o.RowVersion).IsRowVersion().HasColumnName("xmin");

            b.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(o => o.UserId);

            // Keyset-pagination index — every list query filters/sorts on this exact pair.
            b.HasIndex(o => new { o.CreatedAtUtc, o.Id }).HasDatabaseName("ix_orders_cursor");
        });

        modelBuilder.Entity<OrderItem>(b =>
        {
            b.ToTable("order_items");
            b.HasKey(i => i.Id);
            b.Property(i => i.UnitPrice).HasColumnType("numeric(18,2)");
            b.Property(i => i.ProductName).HasMaxLength(200);
        });

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("outbox_messages");
            b.HasKey(o => o.Id);
            b.HasIndex(o => o.ProcessedAtUtc);
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("inbox_messages");
            b.HasKey(i => i.EventId);
        });

        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable("idempotency_records");
            b.HasKey(i => i.IdempotencyKey);
        });

        base.OnModelCreating(modelBuilder);
    }
}

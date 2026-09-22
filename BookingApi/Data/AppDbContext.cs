using BookingApi.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookingApi.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WaitingListEntry> WaitingListEntries => Set<WaitingListEntry>();
    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Resource ----
        builder.Entity<Resource>(e =>
        {
            e.Property(r => r.Name).IsRequired().HasMaxLength(200);
            e.Property(r => r.Location).HasMaxLength(200);
            e.HasIndex(r => r.IsActive);
        });

        // ---- Booking ----
        builder.Entity<Booking>(e =>
        {
            e.Property(b => b.Purpose).HasMaxLength(500);
            e.Property(b => b.QrToken).HasMaxLength(128);
            e.HasIndex(b => b.QrToken).IsUnique();

            // Index to speed up the availability/overlap queries. The actual
            // race-condition protection is the exclusion constraint added by
            // migration SQL (see Data/Sql/001_booking_exclusion_constraint.sql) —
            // this index just makes the application-level pre-check fast.
            e.HasIndex(b => new { b.ResourceId, b.StartTime, b.EndTime });

            e.HasOne(b => b.Resource)
                .WithMany(r => r.Bookings)
                .HasForeignKey(b => b.ResourceId)
                .OnDelete(DeleteBehavior.Restrict); // never cascade-delete booking history

            e.HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Optimistic concurrency: map to Postgres's system column `xmin`,
            // which Postgres increments automatically on every row UPDATE.
            // This has nothing to do with the exclusion constraint above —
            // it protects against two clients editing the SAME existing
            // booking at once, not against two different bookings overlapping.
            e.Property(b => b.Version)
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        });

        // ---- WaitingListEntry ----
        builder.Entity<WaitingListEntry>(e =>
        {
            e.HasIndex(w => new { w.ResourceId, w.StartTime, w.EndTime, w.Status });

            e.HasOne(w => w.Resource)
                .WithMany(r => r.WaitingListEntries)
                .HasForeignKey(w => w.ResourceId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(w => w.User)
                .WithMany(u => u.WaitingListEntries)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- CheckIn ----
        builder.Entity<CheckIn>(e =>
        {
            e.HasOne(c => c.Booking)
                .WithMany()
                .HasForeignKey(c => c.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Notification ----
        builder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

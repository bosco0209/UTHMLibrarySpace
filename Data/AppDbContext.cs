using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using UTHMLibrarySpace.Models;

namespace UTHMLibrarySpace.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<CategoryLimit> CategoryLimits => Set<CategoryLimit>();
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();
    public DbSet<PasswordResetRequest> PasswordResetRequests => Set<PasswordResetRequest>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<CategoryLimit>().Property(l => l.Type).HasConversion<string>();
        b.Entity<CategoryLimit>().HasIndex(l => l.Type).IsUnique();
        b.Entity<PendingRegistration>().HasIndex(p => p.Email);
        b.Entity<PasswordResetRequest>().HasIndex(p => p.Email);
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().HasIndex(u => u.IdNumber).IsUnique();

        b.Entity<Facility>().HasIndex(f => f.Code).IsUnique();
        b.Entity<Facility>().Property(f => f.Type).HasConversion<string>();
        b.Entity<Facility>().Property(f => f.Status).HasConversion<string>();

        b.Entity<Booking>().HasIndex(x => x.Reference).IsUnique();
        b.Entity<Booking>().HasIndex(x => new { x.FacilityId, x.Date });
        b.Entity<Booking>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Booking>().Property(x => x.PaymentStatus).HasConversion<string>();
        b.Entity<Booking>().Property(x => x.Fee).HasConversion<double>();
        b.Entity<Booking>().HasOne(x => x.User).WithMany(u => u.Bookings).HasForeignKey(x => x.UserId);
        b.Entity<Booking>().HasOne(x => x.Facility).WithMany(f => f.Bookings).HasForeignKey(x => x.FacilityId);

        b.Entity<Announcement>().Property(a => a.Kind).HasConversion<string>();
        b.Entity<Notification>().HasIndex(n => new { n.UserId, n.IsRead });

        b.Entity<Feedback>().Property(f => f.Kind).HasConversion<string>();
        b.Entity<Feedback>().Property(f => f.Status).HasConversion<string>();
        b.Entity<Feedback>().HasIndex(f => new { f.UserId, f.CreatedAt });
        b.Entity<Feedback>().HasIndex(f => new { f.Status, f.CreatedAt });

        b.Entity<Feedback>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Feedback>()
            .HasOne(f => f.Facility)
            .WithMany()
            .HasForeignKey(f => f.FacilityId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

using Microsoft.EntityFrameworkCore;
using KidZen.API.Entities;

namespace KidZen.API.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ParentChild> ParentChildren => Set<ParentChild>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<AppRule> AppRules => Set<AppRule>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<TemporaryUnlock> TemporaryUnlocks => Set<TemporaryUnlock>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit Primary Keys
        modelBuilder.Entity<User>().HasKey(u => u.UserId);
        modelBuilder.Entity<ParentChild>().HasKey(pc => pc.Id);
        modelBuilder.Entity<Device>().HasKey(d => d.DeviceId);
        modelBuilder.Entity<Application>().HasKey(a => a.AppId);
        modelBuilder.Entity<AppRule>().HasKey(r => r.RuleId);
        modelBuilder.Entity<UsageRecord>().HasKey(u => u.UsageId);
        modelBuilder.Entity<TemporaryUnlock>().HasKey(t => t.UnlockId);

        // User email unique
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // ParentChild foreign keys
        modelBuilder.Entity<ParentChild>()
            .HasOne(pc => pc.Parent)
            .WithMany(u => u.ParentChildren)
            .HasForeignKey(pc => pc.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ParentChild>()
            .HasOne(pc => pc.Child)
            .WithMany()
            .HasForeignKey(pc => pc.ChildId)
            .OnDelete(DeleteBehavior.Restrict);

        // Device Unique Identifier
        modelBuilder.Entity<Device>()
            .HasIndex(d => d.DeviceIdentifier)
            .IsUnique();

        // AppRule index on DeviceId and PackageName
        modelBuilder.Entity<AppRule>()
            .HasIndex(r => new { r.DeviceId, r.PackageName })
            .IsUnique();

        // UsageRecord index on DeviceId, PackageName, RecordDate
        modelBuilder.Entity<UsageRecord>()
            .HasIndex(u => new { u.DeviceId, u.PackageName, u.RecordDate });
    }
}

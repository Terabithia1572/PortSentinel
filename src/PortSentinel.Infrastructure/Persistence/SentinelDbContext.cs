using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PortSentinel.Domain;

namespace PortSentinel.Infrastructure.Persistence;

public sealed class SentinelDbContext(DbContextOptions<SentinelDbContext> options) : DbContext(options)
{
    public DbSet<RegisteredDevice> Devices => Set<RegisteredDevice>();
    public DbSet<AccessRule> Rules => Set<AccessRule>();
    public DbSet<PolicyRevision> Revisions => Set<PolicyRevision>();
    public DbSet<DeviceConnectionEvent> Connections => Set<DeviceConnectionEvent>();
    public DbSet<AuditEvent> Audit => Set<AuditEvent>();
    public DbSet<ApplicationSettings> Settings => Set<ApplicationSettings>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<RegisteredDevice>(e =>
        {
            e.HasKey(d => d.Id); e.HasIndex(d => d.IdentityKey).IsUnique();
            e.Property(d => d.IdentityKey).HasMaxLength(64); e.Property(d => d.DisplayName).HasMaxLength(80);
            e.OwnsOne(d => d.Identity, owned => { owned.Ignore(i => i.Key); owned.Ignore(i => i.Quality); });
        });
        b.Entity<AccessRule>(e =>
        {
            e.HasKey(r => r.Id); e.HasIndex(r => r.RegisteredDeviceId).IsUnique();
            e.HasOne<RegisteredDevice>().WithOne().HasForeignKey<AccessRule>(r => r.RegisteredDeviceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PolicyRevision>().Property(r => r.State).HasConversion<string>();
        b.Entity<DeviceConnectionEvent>().HasIndex(e => e.OccurredUtc);
        b.Entity<AuditEvent>().HasIndex(e => e.OccurredUtc);
        b.Entity<AuditEvent>().HasIndex(e => e.CorrelationId);
        b.Entity<ApplicationSettings>().HasData(new ApplicationSettings());
    }
}

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<SentinelDbContext>
{
    public SentinelDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<SentinelDbContext>()
        .UseSqlite("Data Source=portsentinel-design.db").Options);
}

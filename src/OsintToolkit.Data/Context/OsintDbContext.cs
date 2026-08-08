using Microsoft.EntityFrameworkCore;
using OsintToolkit.Core.Models;

namespace OsintToolkit.Data.Context;

/// <summary>
/// Entity Framework Core DbContext for SQLite storage.
/// </summary>
public class OsintDbContext : DbContext
{
    public DbSet<Target> Targets => Set<Target>();
    public DbSet<ScanSession> ScanSessions => Set<ScanSession>();
    public DbSet<ScanResult> ScanResults => Set<ScanResult>();

    public OsintDbContext(DbContextOptions<OsintDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Target entity configuration
        modelBuilder.Entity<Target>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Value).IsRequired();
            entity.HasIndex(t => t.Value).IsUnique();
            entity.Property(t => t.Type).HasConversion<string>();
        });

        // ScanSession entity configuration
        modelBuilder.Entity<ScanSession>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Status).HasConversion<string>();
            entity.HasOne(s => s.Target)
                  .WithMany(t => t.ScanSessions)
                  .HasForeignKey(s => s.TargetId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ScanResult entity configuration
        modelBuilder.Entity<ScanResult>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Severity).HasConversion<string>();
            entity.HasOne(r => r.ScanSession)
                  .WithMany(s => s.Results)
                  .HasForeignKey(r => r.ScanSessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

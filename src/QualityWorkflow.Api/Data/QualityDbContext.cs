using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Domain;

namespace QualityWorkflow.Api.Data;

public sealed class QualityDbContext(DbContextOptions<QualityDbContext> options) : DbContext(options)
{
    public DbSet<ProcedureRecord> Procedures => Set<ProcedureRecord>();
    public DbSet<CorrectiveActionItem> CorrectiveActions => Set<CorrectiveActionItem>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var procedure = modelBuilder.Entity<ProcedureRecord>();
        procedure.HasKey(x => x.Id);
        procedure.HasIndex(x => x.LegacyId).IsUnique();
        procedure.HasIndex(x => new { x.Status, x.ReviewDueOn });
        procedure.Property(x => x.LegacyId).HasMaxLength(40);
        procedure.Property(x => x.Title).HasMaxLength(200);
        procedure.Property(x => x.Owner).HasMaxLength(100);
        procedure.Property(x => x.VersionLabel).HasMaxLength(30);
        procedure.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        procedure.Property(x => x.Revision).IsConcurrencyToken();
        procedure.HasMany(x => x.CorrectiveActions)
            .WithOne(x => x.Procedure)
            .HasForeignKey(x => x.ProcedureId)
            .OnDelete(DeleteBehavior.Cascade);

        var action = modelBuilder.Entity<CorrectiveActionItem>();
        action.HasKey(x => x.Id);
        action.HasIndex(x => new { x.Status, x.DueOn });
        action.Property(x => x.Description).HasMaxLength(500);
        action.Property(x => x.Owner).HasMaxLength(100);
        action.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        var audit = modelBuilder.Entity<AuditEntry>();
        audit.HasKey(x => x.Id);
        audit.HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredAt });
        audit.Property(x => x.EntityType).HasMaxLength(60);
        audit.Property(x => x.Action).HasMaxLength(60);
        audit.Property(x => x.Detail).HasMaxLength(1000);
    }
}


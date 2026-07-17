using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Domain;

namespace QualityWorkflow.Api.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(QualityDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Procedures.AnyAsync(cancellationToken))
        {
            return;
        }

        var documentControl = new ProcedureRecord
        {
            Id = Guid.Parse("5f77ecdd-f054-4305-b73c-bd65d84c836d"),
            LegacyId = "PROC-001",
            Title = "Document control and approval",
            Owner = "Quality Manager",
            VersionLabel = "4.2",
            Status = ProcedureStatus.Active,
            ReviewDueOn = new DateOnly(2026, 8, 31)
        };
        documentControl.CorrectiveActions.Add(new CorrectiveActionItem
        {
            Description = "Replace two obsolete document links in the approval checklist.",
            Owner = "Process Owner",
            DueOn = new DateOnly(2026, 8, 15),
            Status = CorrectiveActionStatus.InProgress
        });

        var supplierReview = new ProcedureRecord
        {
            Id = Guid.Parse("c72af118-b187-40ff-b15a-4e01dd91d589"),
            LegacyId = "PROC-014",
            Title = "Supplier qualification review",
            Owner = "Purchasing",
            VersionLabel = "2.0",
            Status = ProcedureStatus.Draft,
            ReviewDueOn = new DateOnly(2026, 10, 15)
        };

        var training = new ProcedureRecord
        {
            Id = Guid.Parse("6501f2a1-0981-42d6-b4b9-d44b73af2d79"),
            LegacyId = "PROC-021",
            Title = "Competence and training records",
            Owner = "People Operations",
            VersionLabel = "3.1",
            Status = ProcedureStatus.Archived,
            ReviewDueOn = new DateOnly(2026, 3, 1)
        };

        dbContext.Procedures.AddRange(documentControl, supplierReview, training);
        dbContext.AuditEntries.AddRange(
            CreateSeedAudit(documentControl),
            CreateSeedAudit(supplierReview),
            CreateSeedAudit(training));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditEntry CreateSeedAudit(ProcedureRecord procedure) => new()
    {
        EntityType = nameof(ProcedureRecord),
        EntityId = procedure.Id,
        Action = "SeededSampleData",
        Detail = $"Legacy ID {procedure.LegacyId}; version {procedure.VersionLabel}; status {procedure.Status}"
    };
}


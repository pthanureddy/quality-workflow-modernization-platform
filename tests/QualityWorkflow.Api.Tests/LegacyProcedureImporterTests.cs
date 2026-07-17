using System.Text;
using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Domain;
using QualityWorkflow.Api.Services;

namespace QualityWorkflow.Api.Tests;

public sealed class LegacyProcedureImporterTests
{
    [Fact]
    public async Task Import_creates_updates_and_rejects_rows_with_an_audit_trail()
    {
        await using var database = await DatabaseTestHelper.CreateAsync();
        database.DbContext.Procedures.Add(new ProcedureRecord
        {
            LegacyId = "PROC-001",
            Title = "Old title",
            Owner = "Old owner",
            VersionLabel = "1.0",
            Status = ProcedureStatus.Draft,
            ReviewDueOn = new DateOnly(2026, 1, 1)
        });
        await database.DbContext.SaveChangesAsync();

        const string csv = """
            legacy_id,title,owner,version,status,review_due_on
            PROC-001,"Document control, approval",Quality Manager,2.0,Active,2026-10-01
            PROC-002,Supplier review,Purchasing,1.0,Draft,2026-12-15
            PROC-003,Invalid row,Quality Manager,1.0,Unknown,2026-12-15
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new LegacyProcedureImporter(database.DbContext).ImportAsync(stream, default);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Rejected);
        Assert.Equal(2, await database.DbContext.Procedures.CountAsync());
        Assert.Equal(2, await database.DbContext.AuditEntries.CountAsync());
        var updated = await database.DbContext.Procedures.SingleAsync(x => x.LegacyId == "PROC-001");
        Assert.Equal("Document control, approval", updated.Title);
        Assert.Equal(ProcedureStatus.Active, updated.Status);
        Assert.Equal(2, updated.Revision);
    }

    [Fact]
    public async Task Import_requires_every_schema_column()
    {
        await using var database = await DatabaseTestHelper.CreateAsync();
        const string csv = "legacy_id,title,owner\nPROC-001,Document control,Quality";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var exception = await Assert.ThrowsAsync<WorkflowValidationException>(() =>
            new LegacyProcedureImporter(database.DbContext).ImportAsync(stream, default));

        Assert.Contains("Missing required columns", exception.Message);
    }

    [Fact]
    public async Task Import_rejects_unclosed_quoted_values()
    {
        await using var database = await DatabaseTestHelper.CreateAsync();
        const string csv = """
            legacy_id,title,owner,version,status,review_due_on
            PROC-001,"Unclosed title,Quality,1.0,Draft,2026-10-01
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var exception = await Assert.ThrowsAsync<WorkflowValidationException>(() =>
            new LegacyProcedureImporter(database.DbContext).ImportAsync(stream, default));

        Assert.Contains("unclosed quoted value", exception.Message);
    }
}


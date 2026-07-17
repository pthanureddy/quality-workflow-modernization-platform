using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Contracts;
using QualityWorkflow.Api.Domain;
using QualityWorkflow.Api.Services;

namespace QualityWorkflow.Api.Tests;

public sealed class ProcedureWorkflowServiceTests
{
    [Fact]
    public async Task Draft_can_be_activated_and_records_revision_and_audit()
    {
        await using var database = await DatabaseTestHelper.CreateAsync();
        var procedure = CreateProcedure(ProcedureStatus.Draft);
        database.DbContext.Procedures.Add(procedure);
        await database.DbContext.SaveChangesAsync();

        var result = await new ProcedureWorkflowService(database.DbContext)
            .ChangeStatusAsync(procedure.Id, new UpdateProcedureStatusRequest("Active", 1), default);

        Assert.Equal("Active", result.Status);
        Assert.Equal(2, result.Revision);
        var audit = await database.DbContext.AuditEntries.SingleAsync();
        Assert.Equal("StatusChanged", audit.Action);
        Assert.Contains("Draft -> Active", audit.Detail);
    }

    [Fact]
    public async Task Stale_revision_returns_conflict_without_mutating_the_procedure()
    {
        await using var database = await DatabaseTestHelper.CreateAsync();
        var procedure = CreateProcedure(ProcedureStatus.Draft);
        procedure.Revision = 4;
        database.DbContext.Procedures.Add(procedure);
        await database.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<WorkflowConflictException>(() =>
            new ProcedureWorkflowService(database.DbContext)
                .ChangeStatusAsync(procedure.Id, new UpdateProcedureStatusRequest("Active", 3), default));

        Assert.Equal(ProcedureStatus.Draft, procedure.Status);
        Assert.Empty(database.DbContext.AuditEntries);
    }

    [Theory]
    [InlineData(ProcedureStatus.Active, "Draft")]
    [InlineData(ProcedureStatus.Archived, "Active")]
    [InlineData(ProcedureStatus.Draft, "Archived")]
    public async Task Unsupported_transitions_are_rejected(ProcedureStatus current, string requested)
    {
        await using var database = await DatabaseTestHelper.CreateAsync();
        var procedure = CreateProcedure(current);
        database.DbContext.Procedures.Add(procedure);
        await database.DbContext.SaveChangesAsync();

        await Assert.ThrowsAsync<WorkflowValidationException>(() =>
            new ProcedureWorkflowService(database.DbContext)
                .ChangeStatusAsync(procedure.Id, new UpdateProcedureStatusRequest(requested, 1), default));
    }

    private static ProcedureRecord CreateProcedure(ProcedureStatus status) => new()
    {
        LegacyId = $"PROC-{Guid.NewGuid():N}"[..13],
        Title = "Document control",
        Owner = "Quality",
        VersionLabel = "1.0",
        Status = status,
        ReviewDueOn = new DateOnly(2026, 10, 1)
    };
}


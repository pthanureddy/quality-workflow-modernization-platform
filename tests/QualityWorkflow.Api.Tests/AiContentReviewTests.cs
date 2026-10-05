using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QualityWorkflow.Api.Ai;
using QualityWorkflow.Api.Data;
using QualityWorkflow.Api.Domain;

namespace QualityWorkflow.Api.Tests;

public sealed class AiContentReviewTests
{
    [Fact]
    public async Task Review_endpoint_returns_source_linked_findings_and_records_metadata_audit()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        var procedure = await AddProcedureAsync(factory);
        var request = new AiContentReviewRequest(
            1,
            [new ContentSection(
                "shutdown.step1",
                "Step 1. Disconnect power. WARNING: verify isolation. TBD torque value.")]);

        var response = await client.PostAsJsonAsync(
            $"/api/procedures/{procedure.Id}/ai-review",
            request);
        var review = await response.Content.ReadFromJsonAsync<AiContentReviewResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(review);
        Assert.Equal("Deterministic", review.Provider);
        Assert.True(review.RequiresHumanReview);
        Assert.Equal(64, review.ContentDigest.Length);
        var finding = Assert.Single(review.Findings);
        Assert.Equal("PlaceholderText", finding.Code);
        Assert.Equal(["shutdown.step1"], finding.EvidenceSectionIds);

        using var scope = factory.Services.CreateScope();
        var audit = await scope.ServiceProvider
            .GetRequiredService<QualityDbContext>()
            .AuditEntries.SingleAsync();
        Assert.Equal("AiContentReviewCompleted", audit.Action);
        Assert.Contains("humanReview=true", audit.Detail);
        Assert.DoesNotContain("TBD torque value", audit.Detail);
    }

    [Fact]
    public async Task Review_endpoint_rejects_stale_procedure_revision()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        var procedure = await AddProcedureAsync(factory, revision: 3);

        var response = await client.PostAsJsonAsync(
            $"/api/procedures/{procedure.Id}/ai-review",
            new AiContentReviewRequest(
                2,
                [new ContentSection("scope", "Step 1. Review the active procedure.")]));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Review_endpoint_rejects_duplicate_section_identifiers()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        var procedure = await AddProcedureAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/procedures/{procedure.Id}/ai-review",
            new AiContentReviewRequest(
                1,
                [
                    new ContentSection("step", "Step 1. Isolate the machine."),
                    new ContentSection("step", "Step 2. Record the result.")
                ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Operations_endpoint_reports_completed_reviews()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        var procedure = await AddProcedureAsync(factory);
        await client.PostAsJsonAsync(
            $"/api/procedures/{procedure.Id}/ai-review",
            new AiContentReviewRequest(
                1,
                [new ContentSection("step1", "Step 1. WARNING: isolate the machine.")]));

        var snapshot = await client.GetFromJsonAsync<AiReviewOperationalSnapshot>(
            "/api/operations/ai-review");

        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.CompletedReviews);
        Assert.Equal(0, snapshot.FailedReviews);
    }

    [Fact]
    public async Task Deterministic_reviewer_marks_missing_safety_and_sequence_for_human_check()
    {
        var reviewer = new DeterministicContentReviewer();

        var review = await reviewer.ReviewAsync(
            "Filter replacement",
            [new ContentSection("instruction", "Replace the filter and record the serial number.")],
            default);

        Assert.Equal(2, review.Findings.Count);
        Assert.Contains(review.Findings, finding => finding.Code == "SafetyMarkerCheck");
        Assert.Contains(review.Findings, finding => finding.Code == "ProcedureSequenceCheck");
    }

    private static async Task<ProcedureRecord> AddProcedureAsync(
        QualityWorkflowApiFactory factory,
        int revision = 1)
    {
        var procedure = new ProcedureRecord
        {
            LegacyId = $"PROC-{Guid.NewGuid():N}"[..13],
            Title = "Machine shutdown",
            Owner = "Operations",
            VersionLabel = "1.0",
            Status = ProcedureStatus.Draft,
            ReviewDueOn = new DateOnly(2026, 12, 1),
            Revision = revision
        };
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
        dbContext.Procedures.Add(procedure);
        await dbContext.SaveChangesAsync();
        return procedure;
    }
}

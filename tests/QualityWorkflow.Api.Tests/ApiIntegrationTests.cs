using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using QualityWorkflow.Api.Contracts;
using QualityWorkflow.Api.Data;
using QualityWorkflow.Api.Domain;

namespace QualityWorkflow.Api.Tests;

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task Audit_endpoint_returns_latest_100_appends_with_preserved_timestamps()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        var timestamp = new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
            dbContext.AuditEntries.AddRange(Enumerable.Range(1, 105).Select(index => new AuditEntry
            {
                EntityType = nameof(ProcedureRecord),
                EntityId = Guid.NewGuid(),
                Action = "SyntheticReview",
                Detail = $"Append {index}",
                OccurredAt = timestamp.AddSeconds(-index)
            }));
            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = await response.Content.ReadFromJsonAsync<List<AuditEntryDto>>();
        Assert.NotNull(entries);
        Assert.Equal(100, entries.Count);
        Assert.Equal(105, entries[0].Id);
        Assert.Equal(6, entries[^1].Id);
        Assert.Equal(timestamp.AddSeconds(-105), entries[0].OccurredAt);
        Assert.Equal(entries.Select(entry => entry.Id).OrderByDescending(id => id),
            entries.Select(entry => entry.Id));
    }

    [Fact]
    public async Task Health_endpoint_reports_the_configured_provider()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();

        var response = await client.GetAsync("/health");
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Sqlite", payload);
    }

    [Fact]
    public async Task Csv_import_is_available_through_the_http_boundary()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        const string csv = """
            legacy_id,title,owner,version,status,review_due_on
            PROC-100,Incident handling,Operations,1.0,Active,2026-09-30
            PROC-101,Internal audit,Quality,1.0,Draft,2026-11-30
            """;
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "procedures.csv");

        var response = await client.PostAsync("/api/procedures/import", multipart);
        var result = await response.Content.ReadFromJsonAsync<ImportResult>();
        var procedures = await client.GetFromJsonAsync<List<ProcedureDto>>("/api/procedures");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(2, result.Created);
        Assert.Equal(2, procedures!.Count);
    }

    [Fact]
    public async Task Status_endpoint_returns_conflict_for_a_stale_revision()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        var procedure = new ProcedureRecord
        {
            LegacyId = "PROC-200",
            Title = "Management review",
            Owner = "Quality",
            VersionLabel = "1.0",
            Status = ProcedureStatus.Draft,
            ReviewDueOn = new DateOnly(2026, 12, 1)
        };
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
            dbContext.Procedures.Add(procedure);
            await dbContext.SaveChangesAsync();
        }

        var accepted = await client.PatchAsJsonAsync(
            $"/api/procedures/{procedure.Id}/status",
            new UpdateProcedureStatusRequest("Active", 1));
        var stale = await client.PatchAsJsonAsync(
            $"/api/procedures/{procedure.Id}/status",
            new UpdateProcedureStatusRequest("Archived", 1));

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Dashboard_counts_procedure_states_and_open_actions()
    {
        using var factory = new QualityWorkflowApiFactory();
        using var client = factory.CreateInitializedClient();
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
            var procedure = new ProcedureRecord
            {
                LegacyId = "PROC-300",
                Title = "Calibration",
                Owner = "Operations",
                VersionLabel = "3.0",
                Status = ProcedureStatus.Active,
                ReviewDueOn = new DateOnly(2020, 1, 1)
            };
            procedure.CorrectiveActions.Add(new CorrectiveActionItem
            {
                Description = "Record missing calibration evidence.",
                Owner = "Operations",
                DueOn = new DateOnly(2026, 8, 1),
                Status = CorrectiveActionStatus.Open
            });
            dbContext.Procedures.Add(procedure);
            await dbContext.SaveChangesAsync();
        }

        var dashboard = await client.GetFromJsonAsync<DashboardDto>("/api/dashboard");

        Assert.NotNull(dashboard);
        Assert.Equal(1, dashboard.TotalProcedures);
        Assert.Equal(1, dashboard.ActiveProcedures);
        Assert.Equal(1, dashboard.OverdueReviews);
        Assert.Equal(1, dashboard.OpenCorrectiveActions);
    }
}


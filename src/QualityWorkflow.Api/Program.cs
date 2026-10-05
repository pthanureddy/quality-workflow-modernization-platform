using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QualityWorkflow.Api.Ai;
using QualityWorkflow.Api.Contracts;
using QualityWorkflow.Api.Data;
using QualityWorkflow.Api.Domain;
using QualityWorkflow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddScoped<LegacyProcedureImporter>();
builder.Services.AddScoped<ProcedureWorkflowService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<AiReviewOptions>(builder.Configuration.GetSection(AiReviewOptions.SectionName));
builder.Services.AddSingleton<DeterministicContentReviewer>();
builder.Services.AddHttpClient<AzureOpenAiContentReviewer>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<AiReviewOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 60));
});
builder.Services.AddScoped<IAiContentReviewer>(serviceProvider =>
    serviceProvider.GetRequiredService<DeterministicContentReviewer>());
builder.Services.AddScoped<IAiContentReviewer>(serviceProvider =>
    serviceProvider.GetRequiredService<AzureOpenAiContentReviewer>());
builder.Services.AddScoped<ContentReviewService>();
builder.Services.AddSingleton<AiReviewTelemetry>();
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("ai-review", limiter =>
{
    limiter.PermitLimit = 10;
    limiter.Window = TimeSpan.FromMinutes(1);
    limiter.QueueLimit = 0;
}));
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
builder.Services.AddDbContext<QualityDbContext>(options =>
{
    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = builder.Configuration.GetConnectionString("QualityWorkflow")
            ?? throw new InvalidOperationException("The QualityWorkflow SQL Server connection string is required.");
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
    }
    else
    {
        options.UseSqlite(
            builder.Configuration.GetConnectionString("QualityWorkflow") ?? "Data Source=quality-workflow.db");
    }
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();

if (!string.Equals(builder.Configuration["DatabaseInitialization"], "None", StringComparison.OrdinalIgnoreCase))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
    if (databaseProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    if (builder.Configuration.GetValue("SeedSampleData", true))
    {
        await DataSeeder.SeedAsync(dbContext);
    }
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    databaseProvider
}));

app.MapGet("/api/procedures", async (
    string? status,
    string? q,
    QualityDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.Procedures.AsNoTracking();
    if (!string.IsNullOrWhiteSpace(status))
    {
        if (!Enum.TryParse<ProcedureStatus>(status, true, out var parsedStatus))
        {
            throw new WorkflowValidationException("Status must be Draft, Active, or Archived.");
        }

        query = query.Where(x => x.Status == parsedStatus);
    }

    if (!string.IsNullOrWhiteSpace(q))
    {
        var search = q.Trim();
        query = query.Where(x =>
            x.LegacyId.Contains(search) ||
            x.Title.Contains(search) ||
            x.Owner.Contains(search));
    }

    var procedures = await query
        .OrderBy(x => x.LegacyId)
        .ToListAsync(cancellationToken);
    return Results.Ok(procedures.Select(ProcedureWorkflowService.ToDto));
});

app.MapGet("/api/dashboard", async (
    QualityDbContext dbContext,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    var total = await dbContext.Procedures.CountAsync(cancellationToken);
    var drafts = await dbContext.Procedures.CountAsync(x => x.Status == ProcedureStatus.Draft, cancellationToken);
    var active = await dbContext.Procedures.CountAsync(x => x.Status == ProcedureStatus.Active, cancellationToken);
    var archived = await dbContext.Procedures.CountAsync(x => x.Status == ProcedureStatus.Archived, cancellationToken);
    var overdue = await dbContext.Procedures.CountAsync(
        x => x.Status == ProcedureStatus.Active && x.ReviewDueOn < today,
        cancellationToken);
    var openActions = await dbContext.CorrectiveActions.CountAsync(
        x => x.Status != CorrectiveActionStatus.Closed,
        cancellationToken);

    return Results.Ok(new DashboardDto(total, drafts, active, archived, overdue, openActions));
});

app.MapPost("/api/procedures/import", async (
    IFormFile file,
    LegacyProcedureImporter importer,
    CancellationToken cancellationToken) =>
{
    if (file.Length == 0)
    {
        throw new WorkflowValidationException("Choose a non-empty CSV file.");
    }

    if (file.Length > 1_000_000)
    {
        throw new WorkflowValidationException("The CSV file must be smaller than 1 MB.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await importer.ImportAsync(stream, cancellationToken));
}).DisableAntiforgery();

app.MapPatch("/api/procedures/{procedureId:guid}/status", async (
    Guid procedureId,
    UpdateProcedureStatusRequest request,
    ProcedureWorkflowService workflowService,
    CancellationToken cancellationToken) =>
    Results.Ok(await workflowService.ChangeStatusAsync(procedureId, request, cancellationToken)));

app.MapPost("/api/procedures/{procedureId:guid}/ai-review", async (
    Guid procedureId,
    AiContentReviewRequest request,
    ContentReviewService reviewService,
    CancellationToken cancellationToken) =>
    Results.Ok(await reviewService.ReviewAsync(procedureId, request, cancellationToken)))
    .RequireRateLimiting("ai-review");

app.MapGet("/api/operations/ai-review", (AiReviewTelemetry telemetry) =>
    Results.Ok(telemetry.Snapshot()));

app.MapGet("/api/audit", async (QualityDbContext dbContext, CancellationToken cancellationToken) =>
{
    var entries = await dbContext.AuditEntries
        .AsNoTracking()
        .OrderByDescending(x => x.OccurredAt)
        .Take(100)
        .Select(x => new AuditEntryDto(
            x.Id,
            x.EntityType,
            x.EntityId,
            x.Action,
            x.Detail,
            x.OccurredAt))
        .ToListAsync(cancellationToken);
    return Results.Ok(entries);
});

app.Run();

public partial class Program;

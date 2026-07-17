namespace QualityWorkflow.Api.Contracts;

public sealed record ProcedureDto(
    Guid Id,
    string LegacyId,
    string Title,
    string Owner,
    string Version,
    string Status,
    DateOnly ReviewDueOn,
    int Revision,
    DateTimeOffset UpdatedAt);

public sealed record UpdateProcedureStatusRequest(string Status, int ExpectedRevision);

public sealed record DashboardDto(
    int TotalProcedures,
    int DraftProcedures,
    int ActiveProcedures,
    int ArchivedProcedures,
    int OverdueReviews,
    int OpenCorrectiveActions);

public sealed record ImportIssue(int RowNumber, string Message);

public sealed record ImportResult(
    int Created,
    int Updated,
    int Rejected,
    IReadOnlyList<ImportIssue> Issues);

public sealed record AuditEntryDto(
    long Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string Detail,
    DateTimeOffset OccurredAt);


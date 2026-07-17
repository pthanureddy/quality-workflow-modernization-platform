namespace QualityWorkflow.Api.Domain;

public enum ProcedureStatus
{
    Draft,
    Active,
    Archived
}

public sealed class ProcedureRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string LegacyId { get; set; }
    public required string Title { get; set; }
    public required string Owner { get; set; }
    public required string VersionLabel { get; set; }
    public ProcedureStatus Status { get; set; }
    public DateOnly ReviewDueOn { get; set; }
    public int Revision { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<CorrectiveActionItem> CorrectiveActions { get; set; } = [];
}


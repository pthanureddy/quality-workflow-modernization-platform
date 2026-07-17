namespace QualityWorkflow.Api.Domain;

public enum CorrectiveActionStatus
{
    Open,
    InProgress,
    Closed
}

public sealed class CorrectiveActionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcedureId { get; set; }
    public required string Description { get; set; }
    public required string Owner { get; set; }
    public DateOnly DueOn { get; set; }
    public CorrectiveActionStatus Status { get; set; }
    public ProcedureRecord Procedure { get; set; } = null!;
}


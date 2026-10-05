namespace QualityWorkflow.Api.Ai;

public sealed class AiReviewOptions
{
    public const string SectionName = "AiReview";

    public string Provider { get; init; } = "Deterministic";
    public string? Endpoint { get; init; }
    public string? Deployment { get; init; }
    public string ApiVersion { get; init; } = "2024-10-21";
    public string? ApiKey { get; init; }
    public int TimeoutSeconds { get; init; } = 15;
}

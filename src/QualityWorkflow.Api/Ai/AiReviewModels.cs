namespace QualityWorkflow.Api.Ai;

public sealed record ContentSection(string Id, string Text);

public sealed record AiContentReviewRequest(
    int ExpectedRevision,
    IReadOnlyList<ContentSection> Sections);

public sealed record AiReviewFinding(
    string Code,
    string Severity,
    string Message,
    IReadOnlyList<string> EvidenceSectionIds,
    double Confidence);

public sealed record AiModelReview(
    string Summary,
    IReadOnlyList<AiReviewFinding> Findings);

public sealed record AiContentReviewResponse(
    Guid ProcedureId,
    int ProcedureRevision,
    string Provider,
    string ContentDigest,
    string Summary,
    IReadOnlyList<AiReviewFinding> Findings,
    bool RequiresHumanReview,
    DateTimeOffset GeneratedAt);

public sealed record AiReviewOperationalSnapshot(
    long CompletedReviews,
    long FailedReviews);

public interface IAiContentReviewer
{
    string ProviderName { get; }

    Task<AiModelReview> ReviewAsync(
        string procedureTitle,
        IReadOnlyList<ContentSection> sections,
        CancellationToken cancellationToken);
}

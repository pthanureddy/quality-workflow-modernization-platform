using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QualityWorkflow.Api.Data;
using QualityWorkflow.Api.Domain;
using QualityWorkflow.Api.Services;

namespace QualityWorkflow.Api.Ai;

public sealed partial class ContentReviewService(
    QualityDbContext dbContext,
    IEnumerable<IAiContentReviewer> reviewers,
    IOptions<AiReviewOptions> options,
    AiReviewTelemetry telemetry,
    TimeProvider timeProvider,
    ILogger<ContentReviewService> logger)
{
    private static readonly HashSet<string> AllowedSeverities =
        new(StringComparer.OrdinalIgnoreCase) { "Low", "Medium", "High" };

    public async Task<AiContentReviewResponse> ReviewAsync(
        Guid procedureId,
        AiContentReviewRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var procedure = await dbContext.Procedures
            .SingleOrDefaultAsync(item => item.Id == procedureId, cancellationToken)
            ?? throw new EntityNotFoundException($"Procedure {procedureId} was not found.");
        if (procedure.Revision != request.ExpectedRevision)
        {
            throw new WorkflowConflictException(
                $"The procedure changed after revision {request.ExpectedRevision}. Reload it before requesting a review.");
        }

        var selectedProvider = options.Value.Provider;
        var reviewer = reviewers.SingleOrDefault(item =>
            item.ProviderName.Equals(selectedProvider, StringComparison.OrdinalIgnoreCase))
            ?? throw new WorkflowValidationException(
                $"AI review provider '{selectedProvider}' is not registered.");
        var digest = ComputeDigest(request.Sections);
        using var activity = telemetry.StartReview(procedureId, reviewer.ProviderName);
        activity?.SetTag("quality_workflow.content_digest", digest);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var modelReview = await reviewer.ReviewAsync(
                procedure.Title,
                request.Sections,
                cancellationToken);
            ValidateProviderOutput(modelReview, request.Sections);

            var generatedAt = timeProvider.GetUtcNow();
            dbContext.AuditEntries.Add(new AuditEntry
            {
                EntityType = nameof(ProcedureRecord),
                EntityId = procedure.Id,
                Action = "AiContentReviewCompleted",
                Detail = $"provider={reviewer.ProviderName}; procedureRevision={procedure.Revision}; " +
                         $"digest={digest}; findings={modelReview.Findings.Count}; humanReview=true",
                OccurredAt = generatedAt
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();
            telemetry.RecordCompleted(reviewer.ProviderName, stopwatch.Elapsed);
            logger.LogInformation(
                "AI content review completed for procedure {ProcedureId} at revision {Revision} " +
                "with provider {Provider}, digest {Digest}, and {FindingCount} findings",
                procedure.Id,
                procedure.Revision,
                reviewer.ProviderName,
                digest,
                modelReview.Findings.Count);

            return new AiContentReviewResponse(
                procedure.Id,
                procedure.Revision,
                reviewer.ProviderName,
                digest,
                modelReview.Summary,
                modelReview.Findings,
                true,
                generatedAt);
        }
        catch
        {
            stopwatch.Stop();
            telemetry.RecordFailed(reviewer.ProviderName, stopwatch.Elapsed);
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    private static void ValidateRequest(AiContentReviewRequest request)
    {
        if (request.ExpectedRevision < 1)
        {
            throw new WorkflowValidationException("ExpectedRevision must be positive.");
        }

        if (request.Sections is null || request.Sections.Count is < 1 or > 20)
        {
            throw new WorkflowValidationException("Provide between 1 and 20 content sections.");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var totalLength = 0;
        foreach (var section in request.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Id) || section.Id.Length > 40 ||
                !SectionIdRegex().IsMatch(section.Id))
            {
                throw new WorkflowValidationException(
                    "Section identifiers must use 1-40 letters, numbers, dots, hyphens, or underscores.");
            }

            if (!identifiers.Add(section.Id))
            {
                throw new WorkflowValidationException($"Section identifier '{section.Id}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(section.Text) || section.Text.Length > 4_000)
            {
                throw new WorkflowValidationException(
                    $"Section '{section.Id}' must contain 1-4000 characters.");
            }

            totalLength += section.Text.Length;
        }

        if (totalLength > 12_000)
        {
            throw new WorkflowValidationException("Combined review content must not exceed 12000 characters.");
        }
    }

    private static void ValidateProviderOutput(
        AiModelReview review,
        IReadOnlyList<ContentSection> sections)
    {
        if (string.IsNullOrWhiteSpace(review.Summary) || review.Summary.Length > 500)
        {
            throw new AiProviderException("The review summary must contain 1-500 characters.");
        }

        if (review.Findings is null || review.Findings.Count > 20)
        {
            throw new AiProviderException("The review returned too many findings.");
        }

        var sectionIds = sections.Select(section => section.Id).ToHashSet(StringComparer.Ordinal);
        var findingCodes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in review.Findings)
        {
            if (string.IsNullOrWhiteSpace(finding.Code) || finding.Code.Length > 60 ||
                !FindingCodeRegex().IsMatch(finding.Code) || !findingCodes.Add(finding.Code))
            {
                throw new AiProviderException("Finding codes must be unique alphanumeric identifiers.");
            }

            if (!AllowedSeverities.Contains(finding.Severity))
            {
                throw new AiProviderException("Finding severity must be Low, Medium, or High.");
            }

            if (string.IsNullOrWhiteSpace(finding.Message) || finding.Message.Length > 500 ||
                finding.Confidence is < 0 or > 1)
            {
                throw new AiProviderException("Finding message or confidence is outside the accepted boundary.");
            }

            if (finding.EvidenceSectionIds is null || finding.EvidenceSectionIds.Count == 0 ||
                finding.EvidenceSectionIds.Any(id => !sectionIds.Contains(id)) ||
                finding.EvidenceSectionIds.Distinct(StringComparer.Ordinal).Count() !=
                finding.EvidenceSectionIds.Count)
            {
                throw new AiProviderException(
                    "Every finding must reference unique section identifiers from the request.");
            }
        }
    }

    private static string ComputeDigest(IReadOnlyList<ContentSection> sections)
    {
        var canonical = string.Join(
            "\n",
            sections.Select(section => $"{section.Id}\0{section.Text.ReplaceLineEndings("\n")}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,40}$")]
    private static partial Regex SectionIdRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]{0,59}$")]
    private static partial Regex FindingCodeRegex();
}

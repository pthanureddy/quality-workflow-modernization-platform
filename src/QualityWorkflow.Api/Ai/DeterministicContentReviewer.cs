using System.Text.RegularExpressions;

namespace QualityWorkflow.Api.Ai;

public sealed partial class DeterministicContentReviewer : IAiContentReviewer
{
    public string ProviderName => "Deterministic";

    public Task<AiModelReview> ReviewAsync(
        string procedureTitle,
        IReadOnlyList<ContentSection> sections,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var findings = new List<AiReviewFinding>();

        var placeholderSections = sections
            .Where(section => PlaceholderRegex().IsMatch(section.Text))
            .Select(section => section.Id)
            .ToArray();
        if (placeholderSections.Length > 0)
        {
            findings.Add(new AiReviewFinding(
                "PlaceholderText",
                "High",
                "Replace placeholder text before the procedure is approved.",
                placeholderSections,
                1.0));
        }

        if (!sections.Any(section => SafetyMarkerRegex().IsMatch(section.Text)))
        {
            findings.Add(new AiReviewFinding(
                "SafetyMarkerCheck",
                "Low",
                "No explicit warning or caution marker was found; verify whether the procedure requires one.",
                sections.Select(section => section.Id).ToArray(),
                0.75));
        }

        if (!sections.Any(section => SequenceMarkerRegex().IsMatch(section.Text)))
        {
            findings.Add(new AiReviewFinding(
                "ProcedureSequenceCheck",
                "Low",
                "No numbered or explicit step marker was found; verify that the action sequence is unambiguous.",
                sections.Select(section => section.Id).ToArray(),
                0.7));
        }

        var normalized = string.Join(" ", sections.Select(section => section.Text.Trim()))
            .ReplaceLineEndings(" ");
        var summary = normalized.Length <= 240 ? normalized : $"{normalized[..237]}...";
        return Task.FromResult(new AiModelReview(
            $"{procedureTitle}: {summary}",
            findings));
    }

    [GeneratedRegex(@"\b(TBD|TODO|FIXME)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\b(warning|caution)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SafetyMarkerRegex();

    [GeneratedRegex(@"\b(step\s+\d+|\d+[.)])", RegexOptions.IgnoreCase)]
    private static partial Regex SequenceMarkerRegex();
}

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace QualityWorkflow.Api.Ai;

public sealed class AiReviewTelemetry
{
    public const string MeterName = "QualityWorkflow.AiReview";
    public const string ActivitySourceName = "QualityWorkflow.AiReview";

    private static readonly ActivitySource Activities = new(ActivitySourceName);
    private readonly Counter<long> completedCounter;
    private readonly Counter<long> failedCounter;
    private readonly Histogram<double> durationHistogram;
    private long completedReviews;
    private long failedReviews;

    public AiReviewTelemetry(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        completedCounter = meter.CreateCounter<long>(
            "quality_workflow.ai_review.completed",
            description: "Completed AI-assisted content reviews.");
        failedCounter = meter.CreateCounter<long>(
            "quality_workflow.ai_review.failed",
            description: "Failed AI-assisted content reviews.");
        durationHistogram = meter.CreateHistogram<double>(
            "quality_workflow.ai_review.duration",
            unit: "s",
            description: "AI-assisted content review duration.");
    }

    public Activity? StartReview(Guid procedureId, string provider)
    {
        var activity = Activities.StartActivity("content-review", ActivityKind.Internal);
        activity?.SetTag("quality_workflow.procedure_id", procedureId);
        activity?.SetTag("gen_ai.provider.name", provider);
        return activity;
    }

    public void RecordCompleted(string provider, TimeSpan duration)
    {
        Interlocked.Increment(ref completedReviews);
        completedCounter.Add(1, new KeyValuePair<string, object?>("provider", provider));
        durationHistogram.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("result", "completed"));
    }

    public void RecordFailed(string provider, TimeSpan duration)
    {
        Interlocked.Increment(ref failedReviews);
        failedCounter.Add(1, new KeyValuePair<string, object?>("provider", provider));
        durationHistogram.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("result", "failed"));
    }

    public AiReviewOperationalSnapshot Snapshot() => new(
        Interlocked.Read(ref completedReviews),
        Interlocked.Read(ref failedReviews));
}

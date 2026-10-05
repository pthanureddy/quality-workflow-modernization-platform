using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using QualityWorkflow.Api.Ai;
using QualityWorkflow.Api.Services;

namespace QualityWorkflow.Api.Tests;

public sealed class AzureOpenAiContentReviewerTests
{
    [Fact]
    public async Task Reviewer_sends_bounded_json_request_and_parses_structured_result()
    {
        var modelReview = new AiModelReview(
            "Safe shutdown sequence.",
            [new AiReviewFinding(
                "AmbiguousReference",
                "Medium",
                "Clarify the referenced switch.",
                ["step2"],
                0.8)]);
        var handler = new StubHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("deployments/content-review/chat/completions", request.RequestUri!.AbsoluteUri);
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("api-key")));
            var requestJson = await request.Content!.ReadAsStringAsync(cancellationToken);
            Assert.Contains("response_format", requestJson);
            Assert.Contains("step2", requestJson);
            return JsonResponse(new
            {
                choices = new[]
                {
                    new { message = new { content = JsonSerializer.Serialize(modelReview) } }
                }
            });
        });
        var reviewer = CreateReviewer(handler);

        var result = await reviewer.ReviewAsync(
            "Shutdown",
            [new ContentSection("step2", "Turn it off.")],
            default);

        Assert.Equal("Safe shutdown sequence.", result.Summary);
        Assert.Equal("AmbiguousReference", Assert.Single(result.Findings).Code);
    }

    [Fact]
    public async Task Reviewer_maps_provider_http_failure_to_safe_exception()
    {
        var reviewer = CreateReviewer(new StubHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests))));

        var exception = await Assert.ThrowsAsync<AiProviderException>(() => reviewer.ReviewAsync(
            "Shutdown",
            [new ContentSection("step1", "Step 1. Stop.")],
            default));

        Assert.Contains("HTTP 429", exception.Message);
    }

    [Fact]
    public async Task Reviewer_rejects_missing_cloud_configuration_before_network_call()
    {
        var reviewer = new AzureOpenAiContentReviewer(
            new HttpClient(new StubHandler((_, _) => throw new InvalidOperationException("Not expected"))),
            Options.Create(new AiReviewOptions { Provider = "AzureOpenAI" }));

        await Assert.ThrowsAsync<AiProviderException>(() => reviewer.ReviewAsync(
            "Shutdown",
            [new ContentSection("step1", "Step 1. Stop.")],
            default));
    }

    private static AzureOpenAiContentReviewer CreateReviewer(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new AiReviewOptions
        {
            Provider = "AzureOpenAI",
            Endpoint = "https://example.openai.azure.com/",
            Deployment = "content-review",
            ApiKey = "test-key",
            ApiVersion = "2024-10-21"
        }));

    private static HttpResponseMessage JsonResponse(object value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(value),
            Encoding.UTF8,
            "application/json")
    };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => callback(request, cancellationToken);
    }
}

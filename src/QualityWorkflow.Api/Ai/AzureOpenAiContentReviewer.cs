using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using QualityWorkflow.Api.Services;

namespace QualityWorkflow.Api.Ai;

public sealed class AzureOpenAiContentReviewer(
    HttpClient httpClient,
    IOptions<AiReviewOptions> options) : IAiContentReviewer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public string ProviderName => "AzureOpenAI";

    public async Task<AiModelReview> ReviewAsync(
        string procedureTitle,
        IReadOnlyList<ContentSection> sections,
        CancellationToken cancellationToken)
    {
        var configuration = options.Value;
        if (!Uri.TryCreate(configuration.Endpoint, UriKind.Absolute, out var endpoint) ||
            string.IsNullOrWhiteSpace(configuration.Deployment) ||
            string.IsNullOrWhiteSpace(configuration.ApiKey))
        {
            throw new AiProviderException(
                "Azure OpenAI review requires endpoint, deployment, and API key configuration.");
        }

        var requestUri = new Uri(
            endpoint,
            $"openai/deployments/{Uri.EscapeDataString(configuration.Deployment)}/chat/completions" +
            $"?api-version={Uri.EscapeDataString(configuration.ApiVersion)}");
        var sourceJson = JsonSerializer.Serialize(new { procedureTitle, sections }, JsonOptions);
        var body = new
        {
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "Review technical procedure content. Return JSON with summary and findings. " +
                              "Each finding needs code, severity (Low, Medium, or High), message, " +
                              "evidenceSectionIds copied only from the input, and confidence from 0 to 1. " +
                              "Do not invent product facts or approve content."
                },
                new { role = "user", content = sourceJson }
            },
            temperature = 0,
            response_format = new { type = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("api-key", configuration.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AiProviderException(
                    $"Azure OpenAI returned HTTP {(int)response.StatusCode}.");
            }

            var envelope = await response.Content.ReadFromJsonAsync<ChatCompletionEnvelope>(
                JsonOptions,
                cancellationToken);
            var content = envelope?.Choices.FirstOrDefault()?.Message.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new AiProviderException("Azure OpenAI returned no review content.");
            }

            return JsonSerializer.Deserialize<AiModelReview>(content, JsonOptions)
                ?? throw new AiProviderException("Azure OpenAI returned an empty review object.");
        }
        catch (AiProviderException)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            throw new AiProviderException("The Azure OpenAI review could not be completed.", exception);
        }
    }

    private sealed record ChatCompletionEnvelope(IReadOnlyList<ChatChoice> Choices);
    private sealed record ChatChoice(ChatMessage Message);
    private sealed record ChatMessage(string Content);
}

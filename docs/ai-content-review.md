# AI-assisted technical-content review

## Purpose

The review route adds a bounded AI integration to the controlled-procedure workflow. It accepts versioned technical-content sections, runs one configured reviewer, validates every returned finding, records metadata-only audit evidence, and always requires a human decision. It does not update or approve procedure content.

`POST /api/procedures/{procedureId}/ai-review`

```json
{
  "expectedRevision": 1,
  "sections": [
    {
      "id": "shutdown.step1",
      "text": "Step 1. Disconnect power. WARNING: verify isolation."
    }
  ]
}
```

The request is limited to 20 sections, 4,000 characters per section, and 12,000 characters overall. Section identifiers must be unique. The procedure revision must still match the caller's view.

## Providers

`Deterministic` is the default and is used by local development and CI. It is a transparent rule-based reviewer, not an LLM. It identifies placeholder markers and asks for human checks when explicit safety or sequence markers are absent.

`AzureOpenAI` uses the configured Azure OpenAI chat-completions endpoint from .NET through `HttpClient`. The prompt requests structured JSON. The service then rejects unknown evidence references, invalid confidence values, unsupported severity labels, duplicate finding codes, oversized output, and malformed JSON.

Configuration is supplied outside source control:

```powershell
$env:AiReview__Provider = "AzureOpenAI"
$env:AiReview__Endpoint = "https://YOUR-RESOURCE.openai.azure.com/"
$env:AiReview__Deployment = "YOUR-DEPLOYMENT"
$env:AiReview__ApiKey = "YOUR-SECRET"
dotnet run --project src/QualityWorkflow.Api
```

The repository does not contain a key and CI does not call a paid model. The Azure API version is configurable because deployment compatibility changes over time.

## Validation and failure behavior

- Procedure revision conflicts return HTTP 409 before a provider call.
- Request-boundary failures return HTTP 400.
- Provider transport, status, and structured-output failures return a generic HTTP 502 problem response.
- Provider output can only cite section identifiers supplied in the request.
- A SHA-256 digest, provider name, procedure revision, finding count, and human-review flag are written to the audit table; source text and model output are not persisted there.
- The response always sets `requiresHumanReview` to `true`.

## Observability

The review path emits structured logs, an `ActivitySource` span, and .NET `System.Diagnostics.Metrics` counters/histograms for completed reviews, failed reviews, and duration. `GET /api/operations/ai-review` exposes process-local completed/failed counts for demonstrations and smoke tests. Production collection still requires an OpenTelemetry or Azure Monitor pipeline and an authorization policy for the operations endpoint.

## Azure deployment blueprint

`infra/azure/main.bicep` defines an Azure Container Apps environment, Log Analytics workspace, container app, managed identity, HTTPS ingress, health probes, secret-backed SQL configuration, optional Azure OpenAI configuration, and HTTP scaling. CI compiles and lints the Bicep file without Azure credentials.

The blueprint has not been deployed to an Azure subscription. Before production use, assign least-privilege managed-identity roles, replace the API key with identity-based Azure OpenAI authentication, provide a managed SQL service and private networking, add authentication/authorization, configure telemetry export and alerts, and rehearse database migration and rollback.

namespace QualityWorkflow.Api.Services;

public sealed class EntityNotFoundException(string message) : Exception(message);

public sealed class WorkflowConflictException(string message) : Exception(message);

public sealed class WorkflowValidationException(string message) : Exception(message);

public sealed class AiProviderException : Exception
{
    public AiProviderException(string message) : base(message)
    {
    }

    public AiProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}


using Microsoft.EntityFrameworkCore;
using QualityWorkflow.Api.Contracts;
using QualityWorkflow.Api.Data;
using QualityWorkflow.Api.Domain;

namespace QualityWorkflow.Api.Services;

public sealed class ProcedureWorkflowService(QualityDbContext dbContext)
{
    public async Task<ProcedureDto> ChangeStatusAsync(
        Guid procedureId,
        UpdateProcedureStatusRequest request,
        CancellationToken cancellationToken)
    {
        var procedure = await dbContext.Procedures
            .SingleOrDefaultAsync(x => x.Id == procedureId, cancellationToken)
            ?? throw new EntityNotFoundException($"Procedure {procedureId} was not found.");

        if (!Enum.TryParse<ProcedureStatus>(request.Status, true, out var requestedStatus))
        {
            throw new WorkflowValidationException("Status must be Draft, Active, or Archived.");
        }

        if (procedure.Revision != request.ExpectedRevision)
        {
            throw new WorkflowConflictException(
                $"The procedure changed after revision {request.ExpectedRevision}. Reload it before updating.");
        }

        if (!IsAllowedTransition(procedure.Status, requestedStatus))
        {
            throw new WorkflowValidationException(
                $"Transition from {procedure.Status} to {requestedStatus} is not allowed.");
        }

        var previousStatus = procedure.Status;
        procedure.Status = requestedStatus;
        procedure.Revision += 1;
        procedure.UpdatedAt = DateTimeOffset.UtcNow;
        dbContext.AuditEntries.Add(new AuditEntry
        {
            EntityType = nameof(ProcedureRecord),
            EntityId = procedure.Id,
            Action = "StatusChanged",
            Detail = $"{previousStatus} -> {requestedStatus}; revision {procedure.Revision}"
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new WorkflowConflictException("The procedure was updated by another request. Reload and retry.");
        }

        return ToDto(procedure);
    }

    public static ProcedureDto ToDto(ProcedureRecord procedure) => new(
        procedure.Id,
        procedure.LegacyId,
        procedure.Title,
        procedure.Owner,
        procedure.VersionLabel,
        procedure.Status.ToString(),
        procedure.ReviewDueOn,
        procedure.Revision,
        procedure.UpdatedAt);

    private static bool IsAllowedTransition(ProcedureStatus current, ProcedureStatus requested) =>
        (current, requested) is
            (ProcedureStatus.Draft, ProcedureStatus.Active) or
            (ProcedureStatus.Active, ProcedureStatus.Archived);
}


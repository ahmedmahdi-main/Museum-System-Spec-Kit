using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyUploadAuditService(IAuditWriter auditWriter)
{
    public Task<string> WriteFileOutcomeAsync(
        PhotographyUploadOperation operation,
        PhotographyUploadFileOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        var actionName = outcome.Status == PhotographyUploadFileOutcomeStatus.RecoveryNeeded
            ? PhotographyAuditActions.StorageConsistencyIssue
            : PhotographyAuditActions.ImageUpload;

        var entityName = outcome.ArtifactImageId.HasValue ? nameof(ArtifactImage) : nameof(PhotographyUploadFileOutcome);
        var entityId = outcome.ArtifactImageId?.ToString() ?? outcome.PhotographyUploadFileOutcomeId.ToString();
        var summary = outcome.Status switch
        {
            PhotographyUploadFileOutcomeStatus.Succeeded => $"Uploaded photography file '{outcome.OriginalFilename}'.",
            PhotographyUploadFileOutcomeStatus.Rejected => $"Rejected photography file '{outcome.OriginalFilename}'.",
            PhotographyUploadFileOutcomeStatus.Failed => $"Failed photography file '{outcome.OriginalFilename}'.",
            PhotographyUploadFileOutcomeStatus.CleanupPending => $"Photography file '{outcome.OriginalFilename}' requires upload cleanup.",
            PhotographyUploadFileOutcomeStatus.RecoveryNeeded => $"Photography file '{outcome.OriginalFilename}' requires storage recovery.",
            _ => $"Recorded photography upload outcome for '{outcome.OriginalFilename}'."
        };

        var changeSummary = string.Join("; ",
        [
            $"UploadOperationId={operation.PhotographyUploadOperationId}",
            $"OperationKind={operation.OperationKind}",
            $"ArtifactId={operation.ArtifactId}",
            $"PhotographySetId={operation.PhotographySetId?.ToString() ?? "<none>"}",
            $"ClientFileOrdinal={outcome.ClientFileOrdinal}",
            $"Status={outcome.Status}",
            $"ArtifactImageId={outcome.ArtifactImageId?.ToString() ?? "<none>"}"
        ]);

        return auditWriter.WriteAsync(new AuditWriteRequest(
            actionName,
            "Photography",
            entityName,
            entityId,
            summary,
            changeSummary), cancellationToken);
    }
}

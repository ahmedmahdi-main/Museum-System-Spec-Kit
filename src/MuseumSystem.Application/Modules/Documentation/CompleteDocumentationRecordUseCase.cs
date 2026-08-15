using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class CompleteDocumentationRecordUseCase(
    IMuseumDbContext dbContext,
    DocumentationAvailabilityService availabilityService,
    IAuditWriter auditWriter,
    IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationRecordSummaryDto>> CompleteDocumentationRecord(CompleteDocumentationRecordRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(request.DocumentationRecordId, cancellationToken);
        if (loaded is null)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Failure(new ValidationIssue("DocumentationRecord.NotFound", "Documentation Record was not found.", nameof(request.DocumentationRecordId)));
        }

        var (record, artifact, version) = loaded.Value;
        if (record.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Conflict("Documentation Record changed. Reload and review the latest Draft before completing.");
        }

        if (record.Status != DocumentationRecordStatus.Draft)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Failure(new ValidationIssue("DocumentationRecord.NotDraft", "Only Draft documentation records can be completed."));
        }

        if (!availabilityService.IsAvailableToDocumentation(artifact))
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Failure(new ValidationIssue("DocumentationRecord.CustodyRequired", "The artifact is not currently held by Documentation."));
        }

        try
        {
            var actor = DocumentationActorIdentity.From(actorContext);
            var values = DocumentationRecordMapper.ToDomainValues(version.Fields, request.Values);
            record.Complete(values, version, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Conflict("Documentation Record changed. Reload and review the latest Draft before completing.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Failure(new ValidationIssue("DocumentationRecord.ValuesInvalid", ex.Message));
        }

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.RecordComplete,
            "Documentation",
            nameof(DocumentationRecord),
            record.DocumentationRecordId.ToString(),
            $"Completed documentation record for artifact {artifact.MuseumNumberDisplay}.",
            "Revision 1 baseline stored on the Documentation Record; custody and movement state unchanged."), cancellationToken);

        return UseCaseResult<DocumentationRecordSummaryDto>.Success(
            DocumentationRecordMapper.ToRecordSummary(record, version),
            "Documentation completed. Revision 1 baseline is available.",
            auditReference);
    }

    private async Task<(DocumentationRecord Record, MuseumSystem.Domain.Modules.ArtifactRegistry.Artifact Artifact, DocumentationTemplateVersion Version)?> LoadAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var record = await dbContext.DocumentationRecords
            .Include(record => record.DocumentationTemplateVersion)
                .ThenInclude(version => version!.Fields)
                    .ThenInclude(field => field.Options)
            .Include(record => record.Revisions)
            .FirstOrDefaultAsync(record => record.DocumentationRecordId == recordId, cancellationToken);

        if (record?.DocumentationTemplateVersion is null)
        {
            return null;
        }

        var artifact = await dbContext.Artifacts.FirstOrDefaultAsync(artifact => artifact.ArtifactId == record.ArtifactId, cancellationToken);
        return artifact is null ? null : (record, artifact, record.DocumentationTemplateVersion);
    }
}

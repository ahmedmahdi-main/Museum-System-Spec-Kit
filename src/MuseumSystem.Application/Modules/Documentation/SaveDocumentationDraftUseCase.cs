using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class SaveDocumentationDraftUseCase(
    IMuseumDbContext dbContext,
    IAuditWriter auditWriter,
    IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationRecordSummaryDto>> SaveDocumentationDraft(SaveDocumentationDraftRequest request, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(request.DocumentationRecordId, cancellationToken);
        if (loaded is null)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Failure(new ValidationIssue("DocumentationRecord.NotFound", "Documentation Record was not found.", nameof(request.DocumentationRecordId)));
        }

        var (record, artifact, version) = loaded.Value;
        if (record.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            return DocumentationConcurrencyHandler.StaleRequest<DocumentationRecordSummaryDto>("Documentation Record changed. Reload and review the latest Draft before saving.");
        }

        if (record.Status != DocumentationRecordStatus.Draft)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Failure(new ValidationIssue("DocumentationRecord.NotDraft", "Only Draft documentation records can be saved."));
        }

        try
        {
            var actor = DocumentationActorIdentity.From(actorContext);
            var values = DocumentationRecordMapper.ToDomainValues(version.Fields, request.Values);
            record.SaveDraft(values, version, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return DocumentationConcurrencyHandler.OptimisticWriteConflict<DocumentationRecordSummaryDto>(
                dbContext,
                ex,
                "Documentation Record changed. Reload and review the latest Draft before saving.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UseCaseResult<DocumentationRecordSummaryDto>.Failure(new ValidationIssue("DocumentationRecord.ValuesInvalid", ex.Message));
        }

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.RecordSaveDraft,
            "Documentation",
            nameof(DocumentationRecord),
            record.DocumentationRecordId.ToString(),
            $"Saved Draft documentation record for artifact {artifact.MuseumNumberDisplay}.",
            $"Values={request.Values.Count}; TemplateVersionId={version.DocumentationTemplateVersionId}"), cancellationToken);

        return UseCaseResult<DocumentationRecordSummaryDto>.Success(
            DocumentationRecordMapper.ToRecordSummary(record, version),
            "Draft documentation saved.",
            auditReference);
    }

    private async Task<(DocumentationRecord Record, MuseumSystem.Domain.Modules.ArtifactRegistry.Artifact Artifact, DocumentationTemplateVersion Version)?> LoadAsync(Guid recordId, CancellationToken cancellationToken)
    {
        var record = await dbContext.DocumentationRecords
            .Include(record => record.DocumentationTemplateVersion)
                .ThenInclude(version => version!.Fields)
                    .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(record => record.DocumentationRecordId == recordId, cancellationToken);

        if (record?.DocumentationTemplateVersion is null)
        {
            return null;
        }

        var artifact = await dbContext.Artifacts.FirstOrDefaultAsync(artifact => artifact.ArtifactId == record.ArtifactId, cancellationToken);
        return artifact is null ? null : (record, artifact, record.DocumentationTemplateVersion);
    }
}

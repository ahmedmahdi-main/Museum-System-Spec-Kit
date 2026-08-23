using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class CorrectCompletedDocumentationUseCase(
    IMuseumDbContext dbContext,
    DocumentationChangeSummaryService changeSummaryService,
    IAuditWriter auditWriter,
    IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<CorrectCompletedDocumentationResultDto>> CorrectCompletedDocumentation(
        CorrectCompletedDocumentationRequest request,
        CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return UseCaseResult<CorrectCompletedDocumentationResultDto>.Failure(
                new ValidationIssue("DocumentationCorrection.ReasonRequired", "A correction reason is required.", nameof(request.Reason)));
        }

        if (reason.Length > 1000)
        {
            return UseCaseResult<CorrectCompletedDocumentationResultDto>.Failure(
                new ValidationIssue("DocumentationCorrection.ReasonTooLong", "Correction reason cannot exceed 1000 characters.", nameof(request.Reason)));
        }

        var record = await dbContext.DocumentationRecords
            .Include(item => item.DocumentationTemplateVersion)!.ThenInclude(version => version!.Fields).ThenInclude(field => field.Options)
            .Include(item => item.Revisions)
            .FirstOrDefaultAsync(item => item.DocumentationRecordId == request.DocumentationRecordId, cancellationToken);

        if (record?.DocumentationTemplateVersion is null)
        {
            return UseCaseResult<CorrectCompletedDocumentationResultDto>.Failure(
                new ValidationIssue("DocumentationRecord.NotFound", "Documentation Record was not found.", nameof(request.DocumentationRecordId)));
        }

        if (record.Status != DocumentationRecordStatus.Completed)
        {
            return UseCaseResult<CorrectCompletedDocumentationResultDto>.Failure(
                new ValidationIssue("DocumentationRecord.NotCompleted", "Only Completed documentation records can be corrected."));
        }

        if (record.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            return DocumentationConcurrencyHandler.StaleRequest<CorrectCompletedDocumentationResultDto>(
                "Documentation Record changed. Reload and review the latest record before correcting.");
        }

        var version = record.DocumentationTemplateVersion;
        DocumentationRevision revision;
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var actor = DocumentationActorIdentity.From(actorContext);
            var values = DocumentationRecordMapper.ToDomainValues(version.Fields, request.Values);
            DocumentationValueRules.ValidateValues(version.Fields, values, requireRequiredFields: true);
            var newValuesJson = DocumentationValueRules.SerializeValues(values);
            var changes = changeSummaryService.Create(record.ValuesJson, newValuesJson, version.Fields);
            revision = record.PrepareCompletedCorrection(values, version, changeSummaryService.Serialize(changes), reason, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
            record.AddPreparedCorrectionRevision(revision);
            dbContext.DocumentationRevisions.Add(revision);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return DocumentationConcurrencyHandler.OptimisticWriteConflict<CorrectCompletedDocumentationResultDto>(
                dbContext,
                ex,
                "Documentation Record changed. Reload and review the latest record before correcting.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or JsonException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return UseCaseResult<CorrectCompletedDocumentationResultDto>.Failure(
                new ValidationIssue("DocumentationRecord.ValuesInvalid", ex.Message));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var artifact = await dbContext.Artifacts.AsNoTracking().FirstOrDefaultAsync(item => item.ArtifactId == record.ArtifactId, cancellationToken);
        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.RecordCorrectCompleted,
            "Documentation",
            nameof(DocumentationRecord),
            record.DocumentationRecordId.ToString(),
            $"Corrected documentation record for artifact {artifact?.MuseumNumberDisplay ?? record.ArtifactId.ToString()} as Revision {revision.RevisionNumber}.",
            $"Revision {revision.RevisionNumber}; reason: {revision.Reason}; custody and movement state unchanged."), cancellationToken);

        return UseCaseResult<CorrectCompletedDocumentationResultDto>.Success(
            new CorrectCompletedDocumentationResultDto(
                DocumentationRecordMapper.ToRecordSummary(record, version),
                revision.RevisionNumber,
                DocumentationRecordMapper.ToValueDtos(record.ValuesJson, version.Fields)),
            $"Documentation correction saved as Revision {revision.RevisionNumber}.",
            auditReference);
    }
}

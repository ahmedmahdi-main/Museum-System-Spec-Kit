using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class CreateDocumentationRecordUseCase(
    IMuseumDbContext dbContext,
    DocumentationTemplateResolver templateResolver,
    DocumentationAvailabilityService availabilityService,
    IAuditWriter auditWriter,
    IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationRecordEditDto>> CreateDocumentationRecord(CreateDocumentationRecordRequest request, CancellationToken cancellationToken = default)
    {
        var artifact = await dbContext.Artifacts
            .Include(artifact => artifact.Category)
            .Include(artifact => artifact.CurrentLocation)
            .FirstOrDefaultAsync(artifact => artifact.ArtifactId == request.ArtifactId, cancellationToken);

        if (artifact is null)
        {
            return UseCaseResult<DocumentationRecordEditDto>.Failure(new ValidationIssue("Artifact.NotFound", "Artifact was not found.", nameof(request.ArtifactId)));
        }

        if (await dbContext.DocumentationRecords.AnyAsync(record => record.ArtifactId == artifact.ArtifactId, cancellationToken))
        {
            return UseCaseResult<DocumentationRecordEditDto>.Failure(new ValidationIssue("DocumentationRecord.AlreadyExists", "The artifact already has a Documentation Record."));
        }

        if (!availabilityService.IsAvailableToDocumentation(artifact))
        {
            return UseCaseResult<DocumentationRecordEditDto>.Failure(new ValidationIssue("DocumentationRecord.CustodyRequired", "The artifact is not currently held by Documentation."));
        }

        var resolution = await templateResolver.ResolveActiveVersionForCategory(artifact.CategoryId, cancellationToken);
        if (resolution is null)
        {
            return UseCaseResult<DocumentationRecordEditDto>.Failure(new ValidationIssue("DocumentationTemplate.ActiveMissing", "No Active documentation template is available for this Artifact Category."));
        }

        DocumentationRecord record;
        try
        {
            var actor = DocumentationActorIdentity.From(actorContext);
            record = DocumentationRecord.Create(artifact.ArtifactId, resolution.Version, actor);
            dbContext.DocumentationRecords.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return DocumentationConcurrencyHandler.OptimisticWriteConflict<DocumentationRecordEditDto>(
                dbContext,
                ex,
                "Active template version changed. Reload and review the latest Documentation workspace before creating a record.");
        }
        catch (DbUpdateException)
        {
            return DocumentationConcurrencyHandler.CompetingWriteConflict<DocumentationRecordEditDto>(
                dbContext,
                "A Documentation Record was created for this artifact first. Reload and review the latest record.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UseCaseResult<DocumentationRecordEditDto>.Failure(new ValidationIssue("DocumentationRecord.CreateInvalid", ex.Message));
        }

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.RecordCreate,
            "Documentation",
            nameof(DocumentationRecord),
            record.DocumentationRecordId.ToString(),
            $"Created Draft documentation record for artifact {artifact.MuseumNumberDisplay}.",
            $"ArtifactId={artifact.ArtifactId}; TemplateVersionId={resolution.Version.DocumentationTemplateVersionId}; Version={resolution.Version.VersionNumber}"), cancellationToken);

        return UseCaseResult<DocumentationRecordEditDto>.Success(
            ToEditDto(artifact, record, resolution.Template, resolution.Version, new DocumentationActionPermissionSet(CanCreate: true, CanEdit: true, CanComplete: true)),
            "Draft documentation record created.",
            auditReference);
    }

    private DocumentationRecordEditDto ToEditDto(
        MuseumSystem.Domain.Modules.ArtifactRegistry.Artifact artifact,
        DocumentationRecord record,
        DocumentationTemplate template,
        DocumentationTemplateVersion version,
        DocumentationActionPermissionSet permissions)
    {
        var isAvailable = availabilityService.IsAvailableToDocumentation(artifact);
        var summary = DocumentationRecordMapper.ToArtifactSummary(artifact, isAvailable, availabilityService.GetUnavailableReason(artifact));
        return new DocumentationRecordEditDto(
            summary,
            DocumentationRecordMapper.ToRecordSummary(record, version),
            TemplateQueryUseCases.ToDetails(template, version, artifact.Category, version.IsUsed),
            DocumentationRecordMapper.ToValueDtos(record.ValuesJson, version.Fields),
            DocumentationRecordMapper.ToActions(record, version, isAvailable, permissions));
    }
}

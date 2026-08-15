using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class GetDocumentationRecordForEditUseCase(
    IMuseumDbContext dbContext,
    DocumentationAvailabilityService availabilityService)
{
    public async Task<UseCaseResult<DocumentationRecordEditDto>> GetDocumentationRecordForEdit(GetDocumentationRecordForEditRequest request, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.DocumentationRecords
            .Include(record => record.DocumentationTemplateVersion)
                .ThenInclude(version => version!.Fields)
                    .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(record => record.DocumentationRecordId == request.DocumentationRecordId, cancellationToken);

        if (record is null || record.DocumentationTemplateVersion is null)
        {
            return UseCaseResult<DocumentationRecordEditDto>.Failure(new ValidationIssue("DocumentationRecord.NotFound", "Documentation Record was not found.", nameof(request.DocumentationRecordId)));
        }

        var artifact = await dbContext.Artifacts
            .Include(artifact => artifact.Category)
            .Include(artifact => artifact.CurrentLocation)
            .FirstOrDefaultAsync(artifact => artifact.ArtifactId == record.ArtifactId, cancellationToken);
        var template = await dbContext.DocumentationTemplates.FindAsync([record.DocumentationTemplateVersion.DocumentationTemplateId], cancellationToken);
        var templateCategory = template is null
            ? null
            : await dbContext.ArtifactCategories.FindAsync([template.ArtifactCategoryId], cancellationToken);

        if (artifact is null || template is null || templateCategory is null)
        {
            return UseCaseResult<DocumentationRecordEditDto>.Failure(new ValidationIssue("DocumentationRecord.ReferenceMissing", "Documentation Record references missing source data."));
        }

        return UseCaseResult<DocumentationRecordEditDto>.Success(ToEditDto(artifact, record, template, templateCategory, request.Permissions));
    }

    private DocumentationRecordEditDto ToEditDto(
        MuseumSystem.Domain.Modules.ArtifactRegistry.Artifact artifact,
        MuseumSystem.Domain.Modules.Documentation.DocumentationRecord record,
        MuseumSystem.Domain.Modules.Documentation.DocumentationTemplate template,
        ArtifactCategory templateCategory,
        DocumentationActionPermissionSet permissions)
    {
        var version = record.DocumentationTemplateVersion!;
        var isAvailable = availabilityService.IsAvailableToDocumentation(artifact);
        var summary = DocumentationRecordMapper.ToArtifactSummary(artifact, isAvailable, availabilityService.GetUnavailableReason(artifact));
        return new DocumentationRecordEditDto(
            summary,
            DocumentationRecordMapper.ToRecordSummary(record, version),
            TemplateQueryUseCases.ToDetails(template, version, templateCategory, version.IsUsed),
            DocumentationRecordMapper.ToValueDtos(record.ValuesJson, version.Fields),
            DocumentationRecordMapper.ToActions(record, version, isAvailable, permissions));
    }
}

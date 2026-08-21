using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class GetDocumentationRevisionDetailsUseCase(
    IMuseumDbContext dbContext,
    DocumentationChangeSummaryService changeSummaryService)
{
    public async Task<UseCaseResult<DocumentationRevisionDetailsDto>> GetDocumentationRevisionDetails(
        GetDocumentationRevisionDetailsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.RevisionNumber < 1)
            return Failure("DocumentationRevision.InvalidNumber", "Revision number must be 1 or greater.");

        var record = await dbContext.DocumentationRecords.AsNoTracking()
            .Include(item => item.DocumentationTemplateVersion)!.ThenInclude(version => version!.Fields).ThenInclude(field => field.Options)
            .Include(item => item.Revisions)
            .FirstOrDefaultAsync(item => item.DocumentationRecordId == request.DocumentationRecordId, cancellationToken);
        if (record?.DocumentationTemplateVersion is null)
            return Failure("DocumentationRecord.NotFound", "Documentation Record was not found.");
        if (record.Status != DocumentationRecordStatus.Completed)
            return Failure("DocumentationRecord.NotCompleted", "Revision details are available only for Completed documentation records.");

        var version = record.DocumentationTemplateVersion;
        var template = await dbContext.DocumentationTemplates.AsNoTracking().FirstOrDefaultAsync(item => item.DocumentationTemplateId == version.DocumentationTemplateId, cancellationToken);
        var category = template is null ? null : await dbContext.ArtifactCategories.AsNoTracking().FirstOrDefaultAsync(item => item.CategoryId == template.ArtifactCategoryId, cancellationToken);
        if (template is null || category is null)
            return Failure("DocumentationRecord.ReferenceMissing", "Documentation Record references missing template data.");
        var templateDto = TemplateQueryUseCases.ToDetails(template, version, category, version.IsUsed);

        if (request.RevisionNumber == 1)
        {
            if (string.IsNullOrWhiteSpace(record.CompletedBaselineValuesJson) || record.CompletedAt is null)
                return Failure("DocumentationRecord.BaselineMissing", "The Revision 1 completion baseline is missing.");
            var baseline = DocumentationRecordMapper.ToValueDtos(record.CompletedBaselineValuesJson, version.Fields);
            return UseCaseResult<DocumentationRevisionDetailsDto>.Success(new(
                record.DocumentationRecordId, 1, true, templateDto, baseline, [], [], [], null, record.CompletedBy, record.CompletedAt.Value));
        }

        var revision = record.Revisions.SingleOrDefault(item => item.RevisionNumber == request.RevisionNumber);
        if (revision is null)
            return Failure("DocumentationRevision.NotFound", "Documentation Revision was not found.");
        if (string.IsNullOrWhiteSpace(revision.Reason))
            return Failure("DocumentationRevision.ReasonMissing", "The correction reason is missing.");

        return UseCaseResult<DocumentationRevisionDetailsDto>.Success(new(
            record.DocumentationRecordId,
            revision.RevisionNumber,
            false,
            templateDto,
            [],
            DocumentationRecordMapper.ToValueDtos(revision.PreviousValuesJson, version.Fields),
            DocumentationRecordMapper.ToValueDtos(revision.NewValuesJson, version.Fields),
            changeSummaryService.Deserialize(revision.ChangeSummaryJson),
            revision.Reason,
            revision.CreatedBy,
            revision.CreatedAt));
    }

    private static UseCaseResult<DocumentationRevisionDetailsDto> Failure(string code, string message) =>
        UseCaseResult<DocumentationRevisionDetailsDto>.Failure(new ValidationIssue(code, message));
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class TemplateQueryUseCases(IMuseumDbContext dbContext)
{
    public async Task<IReadOnlyList<DocumentationTemplateListItemDto>> ListDocumentationTemplates(Guid? artifactCategoryId = null, CancellationToken cancellationToken = default)
    {
        var templates = await dbContext.DocumentationTemplates
            .Include(template => template.Versions)
            .Where(template => artifactCategoryId == null || template.ArtifactCategoryId == artifactCategoryId)
            .OrderBy(template => template.Name)
            .ToListAsync(cancellationToken);

        var categoryIds = templates.Select(template => template.ArtifactCategoryId).Distinct().ToArray();
        var categories = await dbContext.ArtifactCategories
            .Where(category => categoryIds.Contains(category.CategoryId))
            .ToDictionaryAsync(category => category.CategoryId, cancellationToken);

        return templates
            .Select(template => ToListItem(template, categories.GetValueOrDefault(template.ArtifactCategoryId)))
            .OrderBy(template => template.ArtifactCategoryCode)
            .ThenBy(template => template.Name)
            .ToList();
    }

    public async Task<UseCaseResult<DocumentationTemplateVersionDetailsDto>> ViewTemplateVersion(Guid documentationTemplateVersionId, CancellationToken cancellationToken = default)
    {
        var template = await dbContext.DocumentationTemplates
            .Include(template => template.Versions)
                .ThenInclude(version => version.Fields)
                    .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(
                template => template.Versions.Any(version => version.DocumentationTemplateVersionId == documentationTemplateVersionId),
                cancellationToken);

        if (template is null)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.NotFound",
                "Template version was not found.",
                nameof(documentationTemplateVersionId)));
        }

        var category = await dbContext.ArtifactCategories.FindAsync([template.ArtifactCategoryId], cancellationToken);
        var version = template.Versions.Single(version => version.DocumentationTemplateVersionId == documentationTemplateVersionId);
        var isUsed = version.IsUsed || await dbContext.DocumentationRecords.AnyAsync(
            record => record.DocumentationTemplateVersionId == documentationTemplateVersionId,
            cancellationToken);

        return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Success(ToDetails(template, version, category, isUsed));
    }

    internal static DocumentationTemplateVersionDetailsDto ToDetails(
        DocumentationTemplate template,
        DocumentationTemplateVersion version,
        ArtifactCategory? category,
        bool isUsed)
    {
        return new DocumentationTemplateVersionDetailsDto(
            template.DocumentationTemplateId,
            version.DocumentationTemplateVersionId,
            template.ArtifactCategoryId,
            category?.CategoryCode ?? string.Empty,
            category?.NameArabic ?? "Unknown category",
            template.Name,
            template.Description,
            version.VersionNumber,
            version.Status,
            isUsed,
            isUsed || version.Status != DocumentationTemplateVersionStatus.Draft,
            version.ConcurrencyToken,
            version.CreatedAt,
            version.CreatedBy,
            version.LastModifiedAt,
            version.LastModifiedBy,
            version.ActivatedAt,
            version.ActivatedBy,
            version.RetiredAt,
            version.RetiredBy,
            version.Fields
                .OrderBy(field => field.DisplayOrder)
                .ThenBy(field => field.FieldKey)
                .Select(ToFieldDto)
                .ToList());
    }

    private static DocumentationTemplateListItemDto ToListItem(DocumentationTemplate template, ArtifactCategory? category)
    {
        var versions = template.Versions.ToList();
        var latest = versions.OrderByDescending(version => version.VersionNumber).FirstOrDefault();
        var active = versions.SingleOrDefault(version => version.Status == DocumentationTemplateVersionStatus.Active);

        return new DocumentationTemplateListItemDto(
            template.DocumentationTemplateId,
            template.ArtifactCategoryId,
            category?.CategoryCode ?? string.Empty,
            category?.NameArabic ?? "Unknown category",
            template.Name,
            template.Description,
            versions.Count,
            versions.Count(version => version.Status == DocumentationTemplateVersionStatus.Draft),
            versions.Count(version => version.Status == DocumentationTemplateVersionStatus.Active),
            versions.Count(version => version.Status == DocumentationTemplateVersionStatus.Retired),
            latest is null ? null : ToSummary(latest),
            active is null ? null : ToSummary(active),
            versions
                .OrderByDescending(version => version.VersionNumber)
                .Select(ToSummary)
                .ToList());
    }

    internal static DocumentationTemplateVersionSummaryDto ToSummary(DocumentationTemplateVersion version) => new(
        version.DocumentationTemplateVersionId,
        version.VersionNumber,
        version.Status,
        version.IsUsed,
        version.ConcurrencyToken,
        version.CreatedAt,
        version.ActivatedAt,
        version.RetiredAt);

    private static DocumentationTemplateFieldDto ToFieldDto(DocumentationTemplateField field) => new(
        field.DocumentationTemplateFieldId,
        field.FieldKey,
        field.Label,
        field.FieldType,
        field.IsRequired,
        field.DisplayOrder,
        field.Section,
        field.HelpText,
        field.Options
            .OrderBy(option => option.DisplayOrder)
            .ThenBy(option => option.OptionKey)
            .Select(option => new DocumentationTemplateFieldOptionDto(
                option.DocumentationTemplateFieldOptionId,
                option.OptionKey,
                option.Label,
                option.DisplayOrder))
            .ToList());
}

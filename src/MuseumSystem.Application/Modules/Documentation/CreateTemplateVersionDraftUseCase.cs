using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class CreateTemplateVersionDraftUseCase(IMuseumDbContext dbContext, IAuditWriter auditWriter, IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationTemplateVersionDetailsDto>> CreateTemplateVersionDraft(CreateTemplateVersionDraftRequest request, CancellationToken cancellationToken = default)
    {
        var template = await dbContext.DocumentationTemplates
            .Include(template => template.Versions)
                .ThenInclude(version => version.Fields)
                    .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(template => template.DocumentationTemplateId == request.DocumentationTemplateId, cancellationToken);

        if (template is null)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplate.NotFound",
                "Documentation template was not found.",
                nameof(request.DocumentationTemplateId)));
        }

        DocumentationTemplateVersion? sourceVersion = null;
        if (request.SourceTemplateVersionId is not null)
        {
            sourceVersion = template.Versions.FirstOrDefault(version => version.DocumentationTemplateVersionId == request.SourceTemplateVersionId.Value);
            if (sourceVersion is null)
            {
                return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                    "DocumentationTemplateVersion.SourceNotFound",
                    "Source template version was not found in this template family.",
                    nameof(request.SourceTemplateVersionId)));
            }
        }

        var copiedFields = sourceVersion?.Fields
            .OrderBy(field => field.DisplayOrder)
            .Select(CopyField)
            .ToList();

        var actor = DocumentationActorIdentity.From(actorContext);
        var draft = template.CreateDraftVersion(copiedFields, actor);
        dbContext.DocumentationTemplateVersions.Add(draft);
        await dbContext.SaveChangesAsync(cancellationToken);

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.TemplateVersionSaveDraft,
            "Documentation",
            nameof(DocumentationTemplateVersion),
            draft.DocumentationTemplateVersionId.ToString(),
            $"Created Draft template version {draft.VersionNumber}.",
            sourceVersion is null ? "Created empty Draft version." : $"Copied from version {sourceVersion.VersionNumber}."), cancellationToken);

        var category = await dbContext.ArtifactCategories.FindAsync([template.ArtifactCategoryId], cancellationToken);
        return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Success(
            TemplateQueryUseCases.ToDetails(template, draft, category, false),
            "Draft template version created.",
            auditReference);
    }

    private static DocumentationTemplateField CopyField(DocumentationTemplateField field) =>
        DocumentationTemplateField.Create(
            field.FieldKey,
            field.Label,
            field.FieldType,
            field.IsRequired,
            field.DisplayOrder,
            field.Section,
            field.HelpText,
            field.Options
                .OrderBy(option => option.DisplayOrder)
                .Select(option => DocumentationTemplateFieldOption.Create(option.OptionKey, option.Label, option.DisplayOrder))
                .ToList());
}
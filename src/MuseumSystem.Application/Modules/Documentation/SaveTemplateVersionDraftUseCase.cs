using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class SaveTemplateVersionDraftUseCase(IMuseumDbContext dbContext, IAuditWriter auditWriter, IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationTemplateVersionDetailsDto>> SaveTemplateVersionDraft(SaveTemplateVersionDraftRequest request, CancellationToken cancellationToken = default)
    {
        var version = await dbContext.DocumentationTemplateVersions
            .Include(version => version.Fields)
                .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(version => version.DocumentationTemplateVersionId == request.DocumentationTemplateVersionId, cancellationToken);

        if (version is null)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.NotFound",
                "Template version was not found.",
                nameof(request.DocumentationTemplateVersionId)));
        }

        if (version.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            return DocumentationConcurrencyHandler.StaleRequest<DocumentationTemplateVersionDetailsDto>("Template version changed. Reload and review the latest Draft before saving.");
        }

        if (version.Status != DocumentationTemplateVersionStatus.Draft)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.NotDraft",
                "Only Draft template versions can be edited."));
        }

        if (version.IsUsed || await dbContext.DocumentationRecords.AnyAsync(record => record.DocumentationTemplateVersionId == version.DocumentationTemplateVersionId, cancellationToken))
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.UsedImmutable",
                "Used template versions are read-only except retirement status."));
        }

        List<DocumentationTemplateField> fields;
        try
        {
            fields = request.Fields.Select(ToField).ToList();
            DocumentationTemplateRules.ValidateVersionFields(fields);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.InvalidFields",
                ex.Message));
        }

        var oldOptions = version.Fields.SelectMany(field => field.Options).ToList();
        var oldFields = version.Fields.ToList();
        dbContext.DocumentationTemplateFieldOptions.RemoveRange(oldOptions);
        dbContext.DocumentationTemplateFields.RemoveRange(oldFields);

        try
        {
            var actor = DocumentationActorIdentity.From(actorContext);
            version.ReplaceFields(fields, actor);
            dbContext.DocumentationTemplateFields.AddRange(fields);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            return DocumentationConcurrencyHandler.OptimisticWriteConflict<DocumentationTemplateVersionDetailsDto>(
                dbContext,
                ex,
                "Template version changed. Reload and review the latest Draft before saving.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.InvalidFields",
                ex.Message));
        }

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.TemplateVersionSaveDraft,
            "Documentation",
            nameof(DocumentationTemplateVersion),
            version.DocumentationTemplateVersionId.ToString(),
            $"Saved Draft template version {version.VersionNumber}.",
            $"Fields={fields.Count}"), cancellationToken);

        var template = await dbContext.DocumentationTemplates.FindAsync([version.DocumentationTemplateId], cancellationToken);
        var category = template is null ? null : await dbContext.ArtifactCategories.FindAsync([template.ArtifactCategoryId], cancellationToken);
        return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Success(
            TemplateQueryUseCases.ToDetails(template!, version, category, false),
            "Draft template version saved.",
            auditReference);
    }

    private static DocumentationTemplateField ToField(DocumentationTemplateFieldInputDto input) =>
        DocumentationTemplateField.Create(
            input.FieldKey,
            input.Label,
            input.FieldType,
            input.IsRequired,
            input.DisplayOrder,
            input.Section,
            input.HelpText,
            input.Options
                .Select(option => DocumentationTemplateFieldOption.Create(option.OptionKey, option.Label, option.DisplayOrder))
                .ToList());
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class ActivateTemplateVersionUseCase(IMuseumDbContext dbContext, IAuditWriter auditWriter, IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationTemplateVersionDetailsDto>> ActivateTemplateVersion(ActivateTemplateVersionRequest request, CancellationToken cancellationToken = default)
    {
        var template = await dbContext.DocumentationTemplates
            .Include(template => template.Versions)
                .ThenInclude(version => version.Fields)
                    .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(
                template => template.Versions.Any(version => version.DocumentationTemplateVersionId == request.DocumentationTemplateVersionId),
                cancellationToken);

        if (template is null)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.NotFound",
                "Template version was not found.",
                nameof(request.DocumentationTemplateVersionId)));
        }

        var version = template.Versions.Single(version => version.DocumentationTemplateVersionId == request.DocumentationTemplateVersionId);
        var previousActive = template.Versions.SingleOrDefault(candidate =>
            candidate.DocumentationTemplateVersionId != version.DocumentationTemplateVersionId &&
            candidate.Status == DocumentationTemplateVersionStatus.Active);

        if (version.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Conflict("Template version changed. Reload and review before activating.");
        }

        try
        {
            var actor = DocumentationActorIdentity.From(actorContext);
            template.ActivateVersion(version, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Conflict("Template version changed. Reload and review before activating.");
        }
        catch (DbUpdateException)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Conflict("Another template version became active first. Reload and review the latest template state.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.ActivationInvalid",
                ex.Message));
        }

        var successMessage = previousActive is null
            ? $"Template version {version.VersionNumber} activated."
            : $"Previous Active template version {previousActive.VersionNumber} retired atomically and template version {version.VersionNumber} activated.";
        var auditSummary = previousActive is null
            ? $"Activated template version {version.VersionNumber}."
            : $"Activated template version {version.VersionNumber} after retiring previous Active version {previousActive.VersionNumber}.";
        var auditChangeSummary = previousActive is null
            ? "No previous Active version existed."
            : $"Retired previous Active version {previousActive.VersionNumber} atomically; activated version {version.VersionNumber}.";

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.TemplateVersionActivate,
            "Documentation",
            nameof(DocumentationTemplateVersion),
            version.DocumentationTemplateVersionId.ToString(),
            auditSummary,
            auditChangeSummary), cancellationToken);

        var category = await dbContext.ArtifactCategories.FindAsync([template.ArtifactCategoryId], cancellationToken);
        var isUsed = await dbContext.DocumentationRecords.AnyAsync(record => record.DocumentationTemplateVersionId == version.DocumentationTemplateVersionId, cancellationToken);
        return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Success(
            TemplateQueryUseCases.ToDetails(template, version, category, isUsed),
            successMessage,
            auditReference);
    }
}
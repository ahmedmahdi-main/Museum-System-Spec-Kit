using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class RetireTemplateVersionUseCase(IMuseumDbContext dbContext, IAuditWriter auditWriter, IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationTemplateVersionDetailsDto>> RetireTemplateVersion(RetireTemplateVersionRequest request, CancellationToken cancellationToken = default)
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
        if (version.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Conflict("Template version changed. Reload and review before retiring.");
        }

        try
        {
            var actor = DocumentationActorIdentity.From(actorContext);
            template.RetireVersion(version, actor);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Conflict("Template version changed. Reload and review before retiring.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Failure(new ValidationIssue(
                "DocumentationTemplateVersion.RetirementInvalid",
                ex.Message));
        }

        var hasActiveReplacement = template.Versions.Any(candidate => candidate.Status == DocumentationTemplateVersionStatus.Active);
        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.TemplateVersionRetire,
            "Documentation",
            nameof(DocumentationTemplateVersion),
            version.DocumentationTemplateVersionId.ToString(),
            $"Retired template version {version.VersionNumber}.",
            hasActiveReplacement ? "Another Active template version remains." : "Category now has zero Active template versions."), cancellationToken);

        var category = await dbContext.ArtifactCategories.FindAsync([template.ArtifactCategoryId], cancellationToken);
        var isUsed = version.IsUsed || await dbContext.DocumentationRecords.AnyAsync(record => record.DocumentationTemplateVersionId == version.DocumentationTemplateVersionId, cancellationToken);
        return UseCaseResult<DocumentationTemplateVersionDetailsDto>.Success(
            TemplateQueryUseCases.ToDetails(template, version, category, isUsed),
            hasActiveReplacement ? "Template version retired." : "Template version retired. This category now has no Active template version.",
            auditReference);
    }
}
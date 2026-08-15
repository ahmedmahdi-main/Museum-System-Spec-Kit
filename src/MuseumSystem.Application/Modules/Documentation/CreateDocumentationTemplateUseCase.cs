using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class CreateDocumentationTemplateUseCase(IMuseumDbContext dbContext, IAuditWriter auditWriter, IAuditActorContext actorContext)
{
    public async Task<UseCaseResult<DocumentationTemplateListItemDto>> CreateDocumentationTemplate(CreateDocumentationTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var category = await dbContext.ArtifactCategories.FindAsync([request.ArtifactCategoryId], cancellationToken);
        if (category is null)
        {
            return UseCaseResult<DocumentationTemplateListItemDto>.Failure(new ValidationIssue(
                "ArtifactCategory.NotFound",
                "Artifact category was not found.",
                nameof(request.ArtifactCategoryId)));
        }

        if (await dbContext.DocumentationTemplates.AnyAsync(template => template.ArtifactCategoryId == request.ArtifactCategoryId, cancellationToken))
        {
            return UseCaseResult<DocumentationTemplateListItemDto>.Failure(new ValidationIssue(
                "DocumentationTemplate.CategoryAlreadyHasTemplate",
                "This artifact category already has a documentation template family.",
                nameof(request.ArtifactCategoryId)));
        }

        DocumentationTemplate template;
        var actor = DocumentationActorIdentity.From(actorContext);
        try
        {
            template = DocumentationTemplate.Create(request.ArtifactCategoryId, request.Name, request.Description, actor);
        }
        catch (ArgumentException ex)
        {
            return UseCaseResult<DocumentationTemplateListItemDto>.Failure(new ValidationIssue(
                "DocumentationTemplate.Invalid",
                ex.Message));
        }

        dbContext.DocumentationTemplates.Add(template);
        await dbContext.SaveChangesAsync(cancellationToken);

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            DocumentationAuditActions.TemplateCreate,
            "Documentation",
            nameof(DocumentationTemplate),
            template.DocumentationTemplateId.ToString(),
            $"Created documentation template '{template.Name}' for category {category.CategoryCode}.",
            $"ArtifactCategoryId={category.CategoryId}"), cancellationToken);

        var dto = new DocumentationTemplateListItemDto(
            template.DocumentationTemplateId,
            template.ArtifactCategoryId,
            category.CategoryCode,
            category.NameArabic,
            template.Name,
            template.Description,
            0,
            0,
            0,
            0,
            null,
            null,
            []);

        return UseCaseResult<DocumentationTemplateListItemDto>.Success(dto, "Documentation template family created.", auditReference);
    }
}
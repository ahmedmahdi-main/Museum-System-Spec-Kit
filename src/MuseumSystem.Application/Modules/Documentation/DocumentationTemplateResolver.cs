using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class DocumentationTemplateResolver(IMuseumDbContext dbContext)
{
    public async Task<DocumentationTemplateResolution?> ResolveActiveVersionForCategory(Guid artifactCategoryId, CancellationToken cancellationToken = default)
    {
        var template = await dbContext.DocumentationTemplates
            .Include(template => template.Versions)
                .ThenInclude(version => version.Fields)
                    .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(template => template.ArtifactCategoryId == artifactCategoryId, cancellationToken);

        var version = template?.Versions.SingleOrDefault(version => version.Status == DocumentationTemplateVersionStatus.Active);
        return template is null || version is null ? null : new DocumentationTemplateResolution(template, version);
    }
}

public sealed record DocumentationTemplateResolution(
    DocumentationTemplate Template,
    DocumentationTemplateVersion Version);

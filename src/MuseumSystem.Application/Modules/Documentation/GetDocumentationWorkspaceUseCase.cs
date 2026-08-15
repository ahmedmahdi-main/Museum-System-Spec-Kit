using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class GetDocumentationWorkspaceUseCase(
    IMuseumDbContext dbContext,
    DocumentationTemplateResolver templateResolver,
    DocumentationAvailabilityService availabilityService)
{
    public async Task<UseCaseResult<DocumentationWorkspaceDto>> GetDocumentationWorkspace(GetDocumentationWorkspaceRequest request, CancellationToken cancellationToken = default)
    {
        var artifact = await dbContext.Artifacts
            .Include(artifact => artifact.Category)
            .Include(artifact => artifact.CurrentLocation)
            .FirstOrDefaultAsync(artifact => artifact.ArtifactId == request.ArtifactId, cancellationToken);

        if (artifact is null)
        {
            return UseCaseResult<DocumentationWorkspaceDto>.Failure(new ValidationIssue("Artifact.NotFound", "Artifact was not found.", nameof(request.ArtifactId)));
        }

        var workspace = await new DocumentationWorkspaceBuilder(dbContext, templateResolver, availabilityService).Build(artifact, request.Permissions, cancellationToken);
        return UseCaseResult<DocumentationWorkspaceDto>.Success(workspace);
    }
}

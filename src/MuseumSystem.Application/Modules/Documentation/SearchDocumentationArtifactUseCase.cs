using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class SearchDocumentationArtifactUseCase(
    IMuseumDbContext dbContext,
    DocumentationTemplateResolver templateResolver,
    DocumentationAvailabilityService availabilityService)
{
    public async Task<UseCaseResult<DocumentationWorkspaceDto>> SearchDocumentationArtifact(SearchDocumentationArtifactRequest request, CancellationToken cancellationToken = default)
    {
        var museumNumber = request.MuseumNumber.Trim();
        if (string.IsNullOrWhiteSpace(museumNumber))
        {
            return UseCaseResult<DocumentationWorkspaceDto>.Failure(new ValidationIssue("MuseumNumber.Required", "Enter a Museum Number.", nameof(request.MuseumNumber)));
        }

        var artifact = await dbContext.Artifacts
            .Include(artifact => artifact.Category)
            .Include(artifact => artifact.CurrentLocation)
            .FirstOrDefaultAsync(artifact => artifact.MuseumNumberDisplay == museumNumber, cancellationToken);

        if (artifact is null)
        {
            return UseCaseResult<DocumentationWorkspaceDto>.Failure(new ValidationIssue("Artifact.NotFound", "No artifact was found for that Museum Number.", nameof(request.MuseumNumber)));
        }

        var workspace = await new DocumentationWorkspaceBuilder(dbContext, templateResolver, availabilityService).Build(artifact, request.Permissions, cancellationToken);
        return UseCaseResult<DocumentationWorkspaceDto>.Success(workspace);
    }
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

internal sealed class DocumentationWorkspaceBuilder(
    IMuseumDbContext dbContext,
    DocumentationTemplateResolver templateResolver,
    DocumentationAvailabilityService availabilityService)
{
    public async Task<DocumentationWorkspaceDto> Build(Artifact artifact, DocumentationActionPermissionSet permissions, CancellationToken cancellationToken)
    {
        var record = await dbContext.DocumentationRecords
            .Include(record => record.DocumentationTemplateVersion)
                .ThenInclude(version => version!.Fields)
                    .ThenInclude(field => field.Options)
            .FirstOrDefaultAsync(record => record.ArtifactId == artifact.ArtifactId, cancellationToken);

        var isAvailable = availabilityService.IsAvailableToDocumentation(artifact);
        var summary = DocumentationRecordMapper.ToArtifactSummary(artifact, isAvailable, availabilityService.GetUnavailableReason(artifact));

        DocumentationTemplateResolution? activeResolution = null;
        if (record is null)
        {
            activeResolution = await templateResolver.ResolveActiveVersionForCategory(artifact.CategoryId, cancellationToken);
        }

        var activeTemplate = activeResolution is null
            ? null
            : TemplateQueryUseCases.ToDetails(activeResolution.Template, activeResolution.Version, artifact.Category, activeResolution.Version.IsUsed);
        var boundVersion = record?.DocumentationTemplateVersion ?? activeResolution?.Version;
        var actions = DocumentationRecordMapper.ToActions(record, activeResolution?.Version, isAvailable, permissions);
        var status = record is null
            ? DocumentationArtifactDocumentationStatus.None
            : record.Status == DocumentationRecordStatus.Draft
                ? DocumentationArtifactDocumentationStatus.Draft
                : DocumentationArtifactDocumentationStatus.Completed;

        return new DocumentationWorkspaceDto(
            summary,
            status,
            record is null || boundVersion is null ? null : DocumentationRecordMapper.ToRecordSummary(record, boundVersion),
            activeTemplate,
            actions);
    }
}

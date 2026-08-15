using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class SearchDocumentationArtifactUseCaseTests
{
    [Fact]
    public async Task Searches_by_museum_number_and_returns_artifact_summary_with_documentation_status()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage, 7, "Clay vessel");
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        await db.SaveChangesAsync();

        var useCase = new SearchDocumentationArtifactUseCase(db, new DocumentationTemplateResolver(db), new DocumentationAvailabilityService());

        var result = await useCase.SearchDocumentationArtifact(new SearchDocumentationArtifactRequest(
            artifact.MuseumNumberDisplay,
            new DocumentationActionPermissionSet(CanCreate: true, CanEdit: true, CanComplete: true)));

        Assert.True(result.Succeeded);
        Assert.Equal(artifact.ArtifactId, result.Value!.Artifact.ArtifactId);
        Assert.Equal("Clay vessel", result.Value.Artifact.BasicDescription);
        Assert.Equal(DocumentationArtifactDocumentationStatus.None, result.Value.DocumentationStatus);
        Assert.True(result.Value.Artifact.IsAvailableToDocumentation);
        Assert.True(result.Value.Actions.CanCreate);
    }

    [Fact]
    public async Task Missing_museum_number_returns_not_found_failure()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var useCase = new SearchDocumentationArtifactUseCase(db, new DocumentationTemplateResolver(db), new DocumentationAvailabilityService());

        var result = await useCase.SearchDocumentationArtifact(new SearchDocumentationArtifactRequest("CER-999", new DocumentationActionPermissionSet()));

        Assert.False(result.Succeeded);
        Assert.Equal("Artifact.NotFound", result.ValidationIssues[0].Code);
    }
}

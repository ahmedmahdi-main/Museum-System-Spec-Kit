using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class GetDocumentationWorkspaceUseCaseTests
{
    [Fact]
    public async Task Calculates_create_action_from_active_template_custody_and_create_permission()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        await db.SaveChangesAsync();

        var result = await NewUseCase(db).GetDocumentationWorkspace(new GetDocumentationWorkspaceRequest(
            artifact.ArtifactId,
            new DocumentationActionPermissionSet(CanCreate: true)));

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.Actions.CanCreate);
        Assert.Null(result.Value.Actions.CreateBlockedReason);
        Assert.NotNull(result.Value.ActiveTemplateVersion);
    }

    [Fact]
    public async Task Blocks_create_when_no_active_template_and_calculates_draft_actions_from_edit_and_complete_permissions()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        await db.SaveChangesAsync();

        var noTemplate = await NewUseCase(db).GetDocumentationWorkspace(new GetDocumentationWorkspaceRequest(
            artifact.ArtifactId,
            new DocumentationActionPermissionSet(CanCreate: true, CanEdit: true, CanComplete: true)));

        Assert.True(noTemplate.Succeeded);
        Assert.False(noTemplate.Value!.Actions.CanCreate);
        Assert.Contains("No Active", noTemplate.Value.Actions.CreateBlockedReason);

        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "tester");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        var draft = await NewUseCase(db).GetDocumentationWorkspace(new GetDocumentationWorkspaceRequest(
            artifact.ArtifactId,
            new DocumentationActionPermissionSet(CanEdit: true, CanComplete: false)));

        Assert.Equal(DocumentationArtifactDocumentationStatus.Draft, draft.Value!.DocumentationStatus);
        Assert.True(draft.Value.Actions.CanSaveDraft);
        Assert.False(draft.Value.Actions.CanComplete);
        Assert.Contains("complete", draft.Value.Actions.CompleteBlockedReason!, StringComparison.OrdinalIgnoreCase);
    }

    private static GetDocumentationWorkspaceUseCase NewUseCase(Infrastructure.Persistence.MuseumDbContext db) =>
        new(db, new DocumentationTemplateResolver(db), new DocumentationAvailabilityService());
}

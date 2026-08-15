using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationCustodyBoundaryTests
{
    [Fact]
    public async Task Complete_rejected_outside_documentation_custody_without_revision_or_custody_changes()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        artifact.ReturnToStorage(storage);
        var statusBefore = artifact.CurrentStatus;
        var holderBefore = artifact.CurrentHolderType;
        var locationBefore = artifact.CurrentLocationId;
        var movementCountBefore = db.MovementRecords.Count();
        await db.SaveChangesAsync();

        var result = await new CompleteDocumentationRecordUseCase(db, new DocumentationAvailabilityService(), new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext())
            .CompleteDocumentationRecord(new CompleteDocumentationRecordRequest(record.DocumentationRecordId, record.ConcurrencyToken, DocumentationApplicationTestHost.RequiredTextValue()));

        Assert.False(result.Succeeded);
        Assert.Equal("DocumentationRecord.CustodyRequired", result.ValidationIssues[0].Code);
        Assert.Equal(DocumentationRecordStatus.Draft, record.Status);
        Assert.Null(record.CompletedBaselineValuesJson);
        Assert.Empty(db.DocumentationRevisions);
        Assert.Equal(movementCountBefore, db.MovementRecords.Count());
        Assert.Equal(statusBefore, artifact.CurrentStatus);
        Assert.Equal(holderBefore, artifact.CurrentHolderType);
        Assert.Equal(locationBefore, artifact.CurrentLocationId);
        Assert.Equal(ArtifactCurrentStatus.InStorage, artifact.CurrentStatus);
    }
}

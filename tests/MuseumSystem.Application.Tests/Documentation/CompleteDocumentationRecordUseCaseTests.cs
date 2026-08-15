using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class CompleteDocumentationRecordUseCaseTests
{
    [Fact]
    public async Task Rejects_missing_required_fields_then_completes_with_revision_one_baseline()
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
        var audit = new RecordingAuditWriter();
        var useCase = NewUseCase(db, audit);

        var missing = await useCase.CompleteDocumentationRecord(new CompleteDocumentationRecordRequest(record.DocumentationRecordId, record.ConcurrencyToken, []));
        var completed = await useCase.CompleteDocumentationRecord(new CompleteDocumentationRecordRequest(record.DocumentationRecordId, record.ConcurrencyToken, DocumentationApplicationTestHost.RequiredTextValue()));

        Assert.False(missing.Succeeded);
        Assert.True(completed.Succeeded);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.NotNull(record.CompletedBaselineValuesJson);
        Assert.True(completed.Value!.HasRevision1Baseline);
        Assert.Empty(db.DocumentationRevisions);
        Assert.Contains(audit.Requests, request => request.ActionName == DocumentationAuditActions.RecordComplete);
    }

    [Fact]
    public async Task Successful_complete_preserves_artifact_custody_location_and_movement_records()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        db.MovementRecords.Add(MovementRecord.CreateDelivery(Guid.NewGuid(), artifact, MovementRecipientType.DocumentationDivision, "Documentation", "Documentation"));
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        var statusBefore = artifact.CurrentStatus;
        var holderTypeBefore = artifact.CurrentHolderType;
        var holderNameBefore = artifact.CurrentHolderName;
        var currentLocationBefore = artifact.CurrentLocationId;
        var lastStorageBefore = artifact.LastKnownStorageLocationId;
        var movementIdsBefore = db.MovementRecords.Select(movement => movement.MovementId).ToArray();

        var result = await NewUseCase(db).CompleteDocumentationRecord(new CompleteDocumentationRecordRequest(
            record.DocumentationRecordId,
            record.ConcurrencyToken,
            DocumentationApplicationTestHost.RequiredTextValue()));

        Assert.True(result.Succeeded);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.True(result.Value!.HasRevision1Baseline);
        Assert.NotNull(record.CompletedBaselineValuesJson);
        Assert.Equal(statusBefore, artifact.CurrentStatus);
        Assert.Equal(holderTypeBefore, artifact.CurrentHolderType);
        Assert.Equal(holderNameBefore, artifact.CurrentHolderName);
        Assert.Equal(currentLocationBefore, artifact.CurrentLocationId);
        Assert.Equal(lastStorageBefore, artifact.LastKnownStorageLocationId);
        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, artifact.CurrentStatus);
        Assert.Equal(MovementRecipientType.DocumentationDivision.ToString(), artifact.CurrentHolderType);
        Assert.Equal(movementIdsBefore, db.MovementRecords.Select(movement => movement.MovementId).ToArray());
    }

    private static CompleteDocumentationRecordUseCase NewUseCase(Infrastructure.Persistence.MuseumDbContext db, RecordingAuditWriter? audit = null) =>
        new(db, new DocumentationAvailabilityService(), audit ?? new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext());
}

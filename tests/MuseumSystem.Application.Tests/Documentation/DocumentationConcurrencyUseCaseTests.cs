using MuseumSystem.Application.Common;
using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationConcurrencyUseCaseTests
{
    [Fact]
    public void Shared_handler_clears_failed_tracked_changes_before_returning_conflict()
    {
        using var db = DocumentationApplicationTestHost.CreateDbContext();
        DocumentationApplicationTestHost.AddCategory(db, "CLR");
        Assert.True(db.ChangeTracker.HasChanges());

        var result = DocumentationConcurrencyHandler.OptimisticWriteConflict<object>(
            db,
            new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException("Simulated stale write."),
            "Documentation Record changed. Reload and review the latest state before saving.");

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public void Shared_competing_write_handler_clears_failed_tracked_changes_before_returning_conflict()
    {
        using var db = DocumentationApplicationTestHost.CreateDbContext();
        DocumentationApplicationTestHost.AddCategory(db, "CMP");
        Assert.True(db.ChangeTracker.HasChanges());

        var result = DocumentationConcurrencyHandler.CompetingWriteConflict<object>(
            db,
            "A Documentation Record was created for this artifact first. Reload and review the latest record.");

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Stale_draft_save_is_rejected_without_overwriting_latest_draft_or_side_effects()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, artifact, _, audit) = await DraftRecordAsync(db);
        var staleToken = record.ConcurrencyToken;

        record.SaveDraft(ToDomainValues("latest value"), record.DocumentationTemplateVersion!, "other-user");
        await db.SaveChangesAsync();
        var latestValuesJson = record.ValuesJson;
        var latestToken = record.ConcurrencyToken;
        var custody = CaptureCustody(artifact);
        var movementIds = db.MovementRecords.Select(movement => movement.MovementId).ToArray();

        var result = await NewSaveDraftUseCase(db, audit).SaveDocumentationDraft(new SaveDocumentationDraftRequest(
            record.DocumentationRecordId,
            staleToken,
            DocumentationApplicationTestHost.RequiredTextValue("title", "stale submitted value")));

        AssertConcurrencyConflict(result);
        Assert.Equal(latestValuesJson, record.ValuesJson);
        Assert.DoesNotContain("stale submitted value", record.ValuesJson);
        Assert.Equal(DocumentationRecordStatus.Draft, record.Status);
        Assert.Equal(latestToken, record.ConcurrencyToken);
        Assert.Empty(audit.Requests);
        Assert.Equal(custody, CaptureCustody(artifact));
        Assert.Equal(movementIds, db.MovementRecords.Select(movement => movement.MovementId).ToArray());
    }

    [Fact]
    public async Task Stale_complete_is_rejected_without_completion_revision_audit_or_custody_side_effects()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, artifact, _, audit) = await DraftRecordAsync(db);
        var staleToken = record.ConcurrencyToken;

        record.SaveDraft(ToDomainValues("latest draft"), record.DocumentationTemplateVersion!, "other-user");
        await db.SaveChangesAsync();
        var latestValuesJson = record.ValuesJson;
        var latestToken = record.ConcurrencyToken;
        var custody = CaptureCustody(artifact);
        var movementIds = db.MovementRecords.Select(movement => movement.MovementId).ToArray();

        var result = await NewCompleteUseCase(db, audit).CompleteDocumentationRecord(new CompleteDocumentationRecordRequest(
            record.DocumentationRecordId,
            staleToken,
            DocumentationApplicationTestHost.RequiredTextValue("title", "stale completion")));

        AssertConcurrencyConflict(result);
        Assert.Equal(DocumentationRecordStatus.Draft, record.Status);
        Assert.Equal(latestValuesJson, record.ValuesJson);
        Assert.DoesNotContain("stale completion", record.ValuesJson);
        Assert.Null(record.CompletedBy);
        Assert.Null(record.CompletedAt);
        Assert.Null(record.CompletedBaselineValuesJson);
        Assert.Empty(record.Revisions);
        Assert.Empty(db.DocumentationRevisions);
        Assert.Equal(latestToken, record.ConcurrencyToken);
        Assert.Empty(audit.Requests);
        Assert.Equal(custody, CaptureCustody(artifact));
        Assert.Equal(movementIds, db.MovementRecords.Select(movement => movement.MovementId).ToArray());
    }

    [Fact]
    public async Task Stale_completed_correction_is_rejected_without_new_revision_audit_or_custody_side_effects()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var artifact = Assert.IsType<Artifact>(await db.Artifacts.FindAsync(record.ArtifactId));
        var staleToken = record.ConcurrencyToken;

        var firstCorrection = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Authoritative update"));
        Assert.True(firstCorrection.Succeeded);
        audit.Requests.Clear();
        var latestValuesJson = record.ValuesJson;
        var latestToken = record.ConcurrencyToken;
        var revisionIds = db.DocumentationRevisions.Select(revision => revision.DocumentationRevisionId).ToArray();
        var custody = CaptureCustody(artifact);
        var movementIds = db.MovementRecords.Select(movement => movement.MovementId).ToArray();

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, staleToken, PhaseDTestData.Value("good"), "Stale correction"));

        AssertConcurrencyConflict(result);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.Equal(latestValuesJson, record.ValuesJson);
        Assert.DoesNotContain("\"good\"", record.ValuesJson);
        Assert.Single(record.Revisions);
        Assert.Equal(revisionIds, db.DocumentationRevisions.Select(revision => revision.DocumentationRevisionId).ToArray());
        Assert.Equal(latestToken, record.ConcurrencyToken);
        Assert.Empty(audit.Requests);
        Assert.Equal(custody, CaptureCustody(artifact));
        Assert.Equal(movementIds, db.MovementRecords.Select(movement => movement.MovementId).ToArray());
    }

    private static async Task<(DocumentationRecord Record, Artifact Artifact, DocumentationTemplateVersion Version, RecordingAuditWriter Audit)> DraftRecordAsync(
        Infrastructure.Persistence.MuseumDbContext db)
    {
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();
        return (record, artifact, version, new RecordingAuditWriter());
    }

    private static Dictionary<string, DocumentationFieldValue> ToDomainValues(string value) =>
        new(StringComparer.Ordinal)
        {
            ["title"] = DocumentationFieldValue.Text(value)
        };

    private static SaveDocumentationDraftUseCase NewSaveDraftUseCase(Infrastructure.Persistence.MuseumDbContext db, RecordingAuditWriter audit) =>
        new(db, new DocumentationAvailabilityService(), audit, DocumentationApplicationTestHost.ActorContext());

    private static CompleteDocumentationRecordUseCase NewCompleteUseCase(Infrastructure.Persistence.MuseumDbContext db, RecordingAuditWriter audit) =>
        new(db, new DocumentationAvailabilityService(), audit, DocumentationApplicationTestHost.ActorContext());

    private static void AssertConcurrencyConflict<T>(UseCaseResult<T> result)
    {
        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        var message = Assert.Single(result.Messages);
        Assert.Contains("Reload", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("review", message, StringComparison.OrdinalIgnoreCase);
    }

    private static ArtifactCustodySnapshot CaptureCustody(Artifact artifact) =>
        new(
            artifact.CurrentStatus,
            artifact.CurrentHolderType,
            artifact.CurrentHolderName,
            artifact.CurrentLocationId,
            artifact.LastKnownStorageLocationId);

    private sealed record ArtifactCustodySnapshot(
        ArtifactCurrentStatus CurrentStatus,
        string? CurrentHolderType,
        string? CurrentHolderName,
        Guid? CurrentLocationId,
        Guid? LastKnownStorageLocationId);
}

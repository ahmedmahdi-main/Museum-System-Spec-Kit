using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

/// <summary>
/// T111: characterizes the CASE 2 recovery contract for permanent image deletion -
/// object storage deletion succeeds but the final PostgreSQL metadata/audit commit fails.
/// Distinct from the storage-failure (DeleteCleanup) coverage already proven in
/// ArtifactImageDeletionServiceTests; here storage always succeeds and only the
/// ArtifactImageDeletionFinalizationService's own metadata commit is faulted.
/// </summary>
public sealed class PhotographyDeletionRecoveryUseCaseTests
{
    private static readonly DateTimeOffset DeletionRequestedAt = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Storage_delete_succeeds_but_metadata_finalization_fails_returns_finalization_pending_without_full_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var originalObjectKey = image.OriginalObjectKey;
        var derivativeKeys = image.Derivatives.Select(derivative => derivative.ObjectKey).ToArray();
        var (service, storage, faulting) = NewServiceWithFaultingFinalization(db);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.FinalizationPending, result.Outcome);
        Assert.NotEqual(ArtifactImageDeletionOutcome.Completed, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Null(result.AuditReference);
        Assert.Equal(1, faulting.ImageConcurrencyFailuresThrown);

        var deleteCall = Assert.Single(storage.DeleteImageObjectCalls);
        Assert.Equal(originalObjectKey, deleteCall.Original);
        Assert.Equal(derivativeKeys, deleteCall.Derivatives);

        var pendingImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, pendingImage.Status);
        Assert.Null(pendingImage.DeletedAt);
        Assert.Null(pendingImage.DeletedByUserId);

        var audits = await db.AuditEntries.ToListAsync();
        Assert.DoesNotContain(audits, entry =>
            entry.ActionName is PhotographyAuditActions.ImageDeleteByUploaderGrace or PhotographyAuditActions.ImageDeletePrivileged);

        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryType.DeleteCleanup, recovery.OperationType);
        Assert.Equal(image.ArtifactImageId, recovery.ArtifactImageId);
        Assert.Equal(StorageOperationRecoveryStatus.Pending, recovery.Status);
    }

    [Fact]
    public async Task Restart_retry_with_fresh_finalization_service_transitions_delete_pending_to_deleted_without_repeating_storage_delete()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var retryAt = DeletionRequestedAt.AddMinutes(10);

        var (storage, image, retryResult, deletionAudit) = await SeedFailOnceThenRetrySucceedsAsync(
            db, ArtifactImageDeletionMode.UploaderGracePeriod, reason: null, markPrimary: false, retryActorUserId: "recovery-worker", retryAt: retryAt);

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, retryResult.Outcome);
        Assert.True(retryResult.Succeeded);
        Assert.Single(storage.DeleteImageObjectCalls);

        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
        Assert.Equal(ArtifactImageDeletionMode.UploaderGracePeriod, finalImage.DeletionMode);
        Assert.Equal("recovery-worker", finalImage.DeletedByUserId);
        Assert.Equal(retryAt, finalImage.DeletedAt);

        Assert.Equal(image.ArtifactImageId.ToString(), deletionAudit.EntityId);
        Assert.Equal(deletionAudit.AuditEntryId.ToString(), retryResult.AuditReference);

        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, recovery.Status);
        Assert.NotNull(recovery.ResolvedAt);
    }

    [Fact]
    public async Task Idempotent_replay_after_successful_retry_does_not_duplicate_audit_storage_or_deletion_metadata()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var retryAt = DeletionRequestedAt.AddMinutes(10);
        var (storage, image, retryResult, _) = await SeedFailOnceThenRetrySucceedsAsync(
            db, ArtifactImageDeletionMode.UploaderGracePeriod, reason: null, markPrimary: false, retryActorUserId: "recovery-worker", retryAt: retryAt);
        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, retryResult.Outcome);

        // A further restart/replay: a brand new finalization service instance with a different actor and timestamp.
        var replayFinalizationService = new ArtifactImageDeletionFinalizationService(db, new AuditWriter(db, new TestAuditActorContext("replay-worker")));
        var replayAt = retryAt.AddMinutes(30);

        var replayResult = await replayFinalizationService.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "replay-worker", replayAt));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.AlreadyFinalized, replayResult.Outcome);
        Assert.True(replayResult.Succeeded);
        Assert.Null(replayResult.AuditReference);
        Assert.Single(storage.DeleteImageObjectCalls);
        Assert.Equal(1, await db.AuditEntries.CountAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace));

        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
        Assert.Equal("recovery-worker", finalImage.DeletedByUserId);
        Assert.Equal(retryAt, finalImage.DeletedAt);
    }

    [Fact]
    public async Task Privileged_deletion_reason_survives_finalization_failure_and_restart_retry()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var retryAt = DeletionRequestedAt.AddMinutes(15);

        var (_, image, retryResult, deletionAudit) = await SeedFailOnceThenRetrySucceedsAsync(
            db, ArtifactImageDeletionMode.Privileged, reason: "duplicate accession photo", markPrimary: false, retryActorUserId: "supervisor-1", retryAt: retryAt);

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, retryResult.Outcome);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageDeletionMode.Privileged, finalImage.DeletionMode);
        Assert.Equal("duplicate accession photo", finalImage.DeletionReason);
        Assert.Equal(PhotographyAuditActions.ImageDeletePrivileged, deletionAudit.ActionName);
        Assert.Contains("Reason=duplicate accession photo", deletionAudit.ChangeSummary);
        Assert.DoesNotContain("ObjectKey", deletionAudit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", deletionAudit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Primary_image_stays_cleared_through_intent_storage_success_finalization_failure_and_retry()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image, _, initialResult) = await SeedAndFailFinalizationOnceAsync(
            db, ArtifactImageDeletionMode.UploaderGracePeriod, reason: null, markPrimary: true);
        Assert.Equal(ArtifactImageDeletionOutcome.FinalizationPending, initialResult.Outcome);

        var stateAfterFailure = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Null(stateAfterFailure.PrimaryImageId);
        var primaryAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.PrimaryImageChange);
        Assert.Contains($"PreviousPrimaryImageId={image.ArtifactImageId}", primaryAudit.ChangeSummary);
        Assert.Contains("NewPrimaryImageId=<null>", primaryAudit.ChangeSummary);

        var retryFinalizationService = new ArtifactImageDeletionFinalizationService(db, new AuditWriter(db, new TestAuditActorContext("recovery-worker")));
        var retryResult = await retryFinalizationService.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "recovery-worker", DeletionRequestedAt.AddMinutes(20)));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, retryResult.Outcome);
        var stateAfterRetry = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Null(stateAfterRetry.PrimaryImageId);
        Assert.Equal(1, await db.AuditEntries.CountAsync(entry => entry.ActionName == PhotographyAuditActions.PrimaryImageChange));
    }

    [Fact]
    public async Task Custody_movement_and_location_remain_unchanged_through_finalization_failure_and_retry()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        var beforeStatus = artifact.CurrentStatus;
        var beforeHolderType = artifact.CurrentHolderType;
        var beforeHolderName = artifact.CurrentHolderName;
        var beforeLocationId = artifact.CurrentLocationId;
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        await db.SaveChangesAsync();
        var movementCountBefore = await db.MovementRecords.CountAsync();

        var (service, _, _) = NewServiceWithFaultingFinalization(db);
        var initialResult = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));
        Assert.Equal(ArtifactImageDeletionOutcome.FinalizationPending, initialResult.Outcome);

        Assert.Equal(beforeStatus, artifact.CurrentStatus);
        Assert.Equal(beforeHolderType, artifact.CurrentHolderType);
        Assert.Equal(beforeHolderName, artifact.CurrentHolderName);
        Assert.Equal(beforeLocationId, artifact.CurrentLocationId);
        Assert.Equal(movementCountBefore, await db.MovementRecords.CountAsync());

        var retryFinalizationService = new ArtifactImageDeletionFinalizationService(db, new AuditWriter(db, new TestAuditActorContext("recovery-worker")));
        var retryResult = await retryFinalizationService.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "recovery-worker", DeletionRequestedAt.AddMinutes(10)));
        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, retryResult.Outcome);

        var artifactAfter = await db.Artifacts.SingleAsync();
        Assert.Equal(beforeStatus, artifactAfter.CurrentStatus);
        Assert.Equal(beforeHolderType, artifactAfter.CurrentHolderType);
        Assert.Equal(beforeHolderName, artifactAfter.CurrentHolderName);
        Assert.Equal(beforeLocationId, artifactAfter.CurrentLocationId);
        Assert.Equal(movementCountBefore, await db.MovementRecords.CountAsync());
    }

    [Fact]
    public async Task Repeated_finalization_failures_create_multiple_pending_recovery_rows_that_all_resolve_on_eventual_success()
    {
        // Characterizes CASE 2 under repeated failures: TryRecordFinalizationRecoveryAsync records a new
        // StorageOperationRecovery on every failed attempt rather than reusing an open one. This does not
        // violate the T111 contract because ResolveDeleteCleanupRecoveriesAsync resolves every unresolved
        // row for the image on eventual success, and none are ever deleted. Deduplicating the open rows is
        // left to T112/T113/T118; this test only proves the current behavior is safe, not that it is final.
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (storage, _, image, faulting, initialResult) = await SeedAndFailFinalizationOnceAsync(
            db, ArtifactImageDeletionMode.UploaderGracePeriod, reason: null, markPrimary: false);
        Assert.Equal(ArtifactImageDeletionOutcome.FinalizationPending, initialResult.Outcome);
        Assert.Equal(1, await db.StorageOperationRecoveries.CountAsync());

        faulting.ThrowNextImageConcurrency = true;
        var secondAttemptService = new ArtifactImageDeletionFinalizationService(faulting, new AuditWriter(faulting, new TestAuditActorContext("recovery-worker")));
        var secondAttemptResult = await secondAttemptService.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "recovery-worker", DeletionRequestedAt.AddMinutes(5)));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.FinalizationPending, secondAttemptResult.Outcome);
        Assert.Equal(2, faulting.ImageConcurrencyFailuresThrown);

        var recoveriesAfterTwoFailures = await db.StorageOperationRecoveries.ToListAsync();
        Assert.Equal(2, recoveriesAfterTwoFailures.Count);
        Assert.All(recoveriesAfterTwoFailures, recovery =>
        {
            Assert.Equal(image.ArtifactImageId, recovery.ArtifactImageId);
            Assert.Equal(StorageOperationRecoveryType.DeleteCleanup, recovery.OperationType);
            Assert.Equal(StorageOperationRecoveryStatus.Pending, recovery.Status);
        });
        Assert.Equal(2, recoveriesAfterTwoFailures.Select(recovery => recovery.StorageOperationRecoveryId).Distinct().Count());
        Assert.Single(storage.DeleteImageObjectCalls);

        var successFinalizationService = new ArtifactImageDeletionFinalizationService(db, new AuditWriter(db, new TestAuditActorContext("recovery-worker")));
        var successResult = await successFinalizationService.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "recovery-worker", DeletionRequestedAt.AddMinutes(10)));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, successResult.Outcome);
        var recoveriesAfterSuccess = await db.StorageOperationRecoveries.ToListAsync();
        Assert.Equal(2, recoveriesAfterSuccess.Count);
        Assert.All(recoveriesAfterSuccess, recovery => Assert.Equal(StorageOperationRecoveryStatus.Resolved, recovery.Status));
        Assert.Equal(1, await db.AuditEntries.CountAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace));
    }

    [Fact]
    public void Deletion_and_finalization_results_never_expose_recovery_or_storage_identifiers()
    {
        AssertNoForbiddenMembers(typeof(ArtifactImageDeletionResult), [
            "ObjectKey", "Bucket", "Endpoint", "Minio", "Presigned", "RecoveryId", "StorageOperationRecoveryId", "FailureSummary"]);
        AssertNoForbiddenMembers(typeof(ArtifactImageDeletionFinalizationResult), [
            "ObjectKey", "Bucket", "Endpoint", "Minio", "Presigned", "RecoveryId", "StorageOperationRecoveryId", "FailureSummary"]);
    }

    private static void AssertNoForbiddenMembers(Type type, IReadOnlyCollection<string> forbiddenFragments)
    {
        var memberNames = type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.Name ?? string.Empty))
            .Concat(type.GetProperties().Select(property => property.Name))
            .ToArray();

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Seeds an Available image (with derivatives) and, when requested, an ArtifactPhotographyState that
    /// designates it as the current Primary, then drives one real DeleteAsync call through a storage that
    /// always succeeds and a finalization service whose own metadata commit is faulted exactly once. This
    /// reproduces CASE 2: storage deletion already happened, only PostgreSQL finalization failed.
    /// </summary>
    private static async Task<(ScriptedArtifactImageStorage Storage, Artifact Artifact, ArtifactImage Image, FaultingPhotographyManagementDbContext Faulting, ArtifactImageDeletionResult InitialResult)> SeedAndFailFinalizationOnceAsync(
        MuseumDbContext db,
        ArtifactImageDeletionMode mode,
        string? reason,
        bool markPrimary)
    {
        var (artifact, _, image) = SeedAvailableImage(db);
        if (markPrimary)
        {
            SeedPrimaryState(db, artifact, image);
        }

        await db.SaveChangesAsync();
        var (service, storage, faulting) = NewServiceWithFaultingFinalization(db);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, mode, reason, "photographer-1", DeletionRequestedAt));

        return (storage, artifact, image, faulting, result);
    }

    /// <summary>
    /// Builds on <see cref="SeedAndFailFinalizationOnceAsync"/> by then simulating an application restart:
    /// a brand new ArtifactImageDeletionFinalizationService instance, bound directly to the persisted
    /// authoritative state (not the faulted wrapper), retries metadata-only finalization to success.
    /// </summary>
    private static async Task<(ScriptedArtifactImageStorage Storage, ArtifactImage Image, ArtifactImageDeletionFinalizationResult RetryResult, AuditEntry DeletionAudit)> SeedFailOnceThenRetrySucceedsAsync(
        MuseumDbContext db,
        ArtifactImageDeletionMode mode,
        string? reason,
        bool markPrimary,
        string retryActorUserId,
        DateTimeOffset retryAt)
    {
        var (storage, _, image, _, initialResult) = await SeedAndFailFinalizationOnceAsync(db, mode, reason, markPrimary);
        if (initialResult.Outcome != ArtifactImageDeletionOutcome.FinalizationPending)
        {
            throw new InvalidOperationException("Test arrangement expected the initial finalization attempt to fail.");
        }

        var retryFinalizationService = new ArtifactImageDeletionFinalizationService(db, new AuditWriter(db, new TestAuditActorContext(retryActorUserId)));
        var retryResult = await retryFinalizationService.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, mode, retryActorUserId, retryAt));

        var expectedAction = mode == ArtifactImageDeletionMode.Privileged
            ? PhotographyAuditActions.ImageDeletePrivileged
            : PhotographyAuditActions.ImageDeleteByUploaderGrace;
        var deletionAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == expectedAction);

        return (storage, image, retryResult, deletionAudit);
    }

    /// <summary>
    /// Wires an ArtifactImageDeletionService whose intent-processing dbContext/auditWriter are the plain,
    /// unfaulted context (so DeletePending intent and any Primary-clearing audit commit normally), but whose
    /// injected finalization service is bound to a context that fails its own metadata SaveChangesAsync
    /// exactly once. This isolates the fault to CASE 2 (post storage-success finalization failure) without
    /// disturbing the earlier intent-persistence step, which also modifies ArtifactImage.
    /// </summary>
    private static (ArtifactImageDeletionService Service, ScriptedArtifactImageStorage Storage, FaultingPhotographyManagementDbContext Faulting) NewServiceWithFaultingFinalization(
        MuseumDbContext db,
        string actorUserId = "photographer-1")
    {
        var actorContext = new TestAuditActorContext(actorUserId);
        var intentAuditWriter = new AuditWriter(db, actorContext);
        var storage = new ScriptedArtifactImageStorage();
        var faulting = new FaultingPhotographyManagementDbContext(db) { ThrowNextImageConcurrency = true };
        var finalizationService = new ArtifactImageDeletionFinalizationService(faulting, new AuditWriter(faulting, actorContext));
        var service = new ArtifactImageDeletionService(db, intentAuditWriter, storage, finalizationService);
        return (service, storage, faulting);
    }

    private static (Artifact Artifact, PhotographySet Set, ArtifactImage Image) SeedAvailableImage(MuseumDbContext db)
    {
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);

        image.AddDerivative(ArtifactImageDerivative.Create(
            image.ArtifactImageId,
            ImageDerivativeKind.Thumbnail,
            ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/thumbnail.jpg"),
            "image/jpeg",
            32,
            120,
            90));
        image.AddDerivative(ArtifactImageDerivative.Create(
            image.ArtifactImageId,
            ImageDerivativeKind.Preview,
            ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/preview.jpg"),
            "image/jpeg",
            64,
            640,
            480));

        return (artifact, set, image);
    }

    private static ArtifactPhotographyState SeedPrimaryState(MuseumDbContext db, Artifact artifact, ArtifactImage image)
    {
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        return state;
    }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

/// <summary>
/// T112: characterizes the internal/system StorageOperationRecovery retry use case (T118) -
/// retry state transitions, idempotency, concurrency, audit behavior, staff-safe messages,
/// and the absence of a sixth Photography permission or any authorization dependency.
/// </summary>
public sealed class StorageOperationRecoveryUseCaseTests
{
    private static readonly DateTimeOffset DeletionRequestedAt = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RetryAt = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    // A. Pending UploadCleanup; all object deletes succeed; Pending -> Retrying -> Resolved.
    [Fact]
    public async Task A_pending_upload_cleanup_all_deletes_succeed_resolves_through_retrying()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var keys = new[] { Key("orphan-1"), Key("orphan-2") };
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, keys, "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, storage) = NewUseCase(RetryAt, db);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        Assert.True(result.Succeeded);
        var persisted = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, persisted.Status);
        Assert.Equal(RetryAt, persisted.LastAttemptedAt);
        Assert.Equal(RetryAt, persisted.ResolvedAt);
        Assert.Equal(keys, storage.DeleteObjectCalls);
        var actions = (await db.AuditEntries.ToListAsync()).Select(entry => entry.ActionName).ToArray();
        Assert.Contains(PhotographyAuditActions.StorageRecoveryRetry, actions);
        Assert.Contains(PhotographyAuditActions.StorageRecoveryResolved, actions);
    }

    // B. UploadCleanup object already NotFound; treated as successful idempotent cleanup.
    [Fact]
    public async Task B_upload_cleanup_object_already_not_found_is_idempotent_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("already-gone");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.OverrideDelete(key, ArtifactImageStorageResultKind.NotFound);
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        Assert.Single(storage.DeleteObjectCalls);
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, (await db.StorageOperationRecoveries.SingleAsync()).Status);
    }

    // C. UploadCleanup with multiple keys; already-absent and successful-delete mixture resolves.
    [Fact]
    public async Task C_upload_cleanup_mixture_of_absent_and_deleted_keys_resolves()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var absentKey = Key("absent");
        var deletedKey = Key("deleted");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [absentKey, deletedKey], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.OverrideDelete(absentKey, ArtifactImageStorageResultKind.NotFound);
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        Assert.Equal(2, storage.DeleteObjectCalls.Count);
    }

    // D. Storage failure: becomes FailedNeedsAttention; result is not success; safe message; no raw internals.
    [Fact]
    public async Task D_upload_cleanup_storage_failure_becomes_failed_needs_attention_with_safe_message()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("unretryable");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.OverrideDelete(key, ArtifactImageStorageResultKind.RetryableFailure);
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.RetryFailed, result.Outcome);
        Assert.False(result.Succeeded);
        AssertSafeMessage(result.StaffFacingMessage);
        Assert.Equal(StorageOperationRecoveryStatus.FailedNeedsAttention, (await db.StorageOperationRecoveries.SingleAsync()).Status);
    }

    // E. Retry FailedNeedsAttention: FailedNeedsAttention -> Retrying -> Resolved.
    [Fact]
    public async Task E_retry_of_failed_needs_attention_recovery_resolves()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("retry-me");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        recovery.MarkRetrying(DeletionRequestedAt);
        recovery.MarkFailedNeedsAttention(DeletionRequestedAt, "Previous attempt failed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, _) = NewUseCase(RetryAt, db);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, (await db.StorageOperationRecoveries.SingleAsync()).Status);
    }

    // F. Resolved retry: AlreadyResolved; no storage calls; no retry/resolved duplicate audit.
    [Fact]
    public async Task F_retry_of_already_resolved_recovery_short_circuits_without_storage_or_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("already-resolved");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        recovery.MarkRetrying(DeletionRequestedAt);
        recovery.MarkResolved(DeletionRequestedAt);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, storage) = NewUseCase(RetryAt, db);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.AlreadyResolved, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Empty(storage.DeleteObjectCalls);
        Assert.Empty(storage.StatCalls);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    // G. Missing recovery: controlled NotFound.
    [Fact]
    public async Task G_missing_recovery_returns_not_found()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (useCase, storage) = NewUseCase(RetryAt, db);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(Guid.NewGuid()));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.NotFound, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Empty(storage.DeleteObjectCalls);
    }

    // H. Recovery concurrency conflict: controlled Conflict; no silent overwrite.
    [Fact]
    public async Task H_concurrency_conflict_on_mark_retrying_returns_conflict_without_overwrite()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("contended");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var faulting = new RecoveryConcurrencyFaultingDbContext(db) { ThrowNextRecoveryConcurrency = true };
        var (useCase, storage) = NewUseCase(RetryAt, db, persistenceContext: faulting);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Conflict, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Equal(1, faulting.RecoveryConcurrencyFailuresThrown);
        Assert.Empty(storage.DeleteObjectCalls);
        var unchanged = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.Pending, unchanged.Status);
    }

    // I. Retry audit: actual attempt writes Photography.Storage.RecoveryRetry.
    [Fact]
    public async Task I_real_retry_attempt_writes_recovery_retry_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("audited");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, _) = NewUseCase(RetryAt, db);

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        var retryAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.StorageRecoveryRetry);
        Assert.Equal(recovery.StorageOperationRecoveryId.ToString(), retryAudit.EntityId);
    }

    // J. Resolved audit: successful completion writes Photography.Storage.RecoveryResolved.
    [Fact]
    public async Task J_successful_completion_writes_recovery_resolved_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("resolved-audit");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, _) = NewUseCase(RetryAt, db);

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        var resolvedAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.StorageRecoveryResolved);
        Assert.Equal(recovery.StorageOperationRecoveryId.ToString(), resolvedAudit.EntityId);
    }

    // K. Failed/manual attention audit: safe Photography.Storage.ConsistencyIssue.
    [Fact]
    public async Task K_failed_recovery_writes_consistency_issue_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("consistency-issue");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.OverrideDelete(key, ArtifactImageStorageResultKind.PermanentFailure);
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        var consistencyAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.StorageConsistencyIssue);
        Assert.Equal(recovery.StorageOperationRecoveryId.ToString(), consistencyAudit.EntityId);
    }

    // L. Audit safety: no object key/bucket/endpoint/provider/raw exception/path in Summary or ChangeSummary.
    [Fact]
    public async Task L_audit_entries_never_expose_storage_internals()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("secret-object-key-fragment");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.OverrideDelete(key, ArtifactImageStorageResultKind.PermanentFailure);
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        var audits = await db.AuditEntries.Where(entry => entry.ModuleName == "Photography").ToListAsync();
        Assert.NotEmpty(audits);
        foreach (var audit in audits)
        {
            AssertSafeText(audit.Summary);
            AssertSafeText(audit.ChangeSummary ?? string.Empty);
        }
    }

    // M. Result reflection: no ObjectKeys/FailureSummary/bucket/endpoint/provider/MinIO/presigned/path properties.
    [Fact]
    public void M_result_never_exposes_storage_or_operational_internals()
    {
        AssertNoForbiddenMembers(typeof(StorageOperationRecoveryRetryResult), [
            "ObjectKey", "ObjectKeys", "Bucket", "Endpoint", "Minio", "Presigned", "FailureSummary", "Path", "Credential"]);
        AssertNoForbiddenMembers(typeof(StorageOperationRecoveryRetryCommand), [
            "ObjectKey", "ObjectKeys", "Bucket", "Endpoint", "Minio", "Presigned", "FailureSummary", "Path", "Credential"]);
    }

    // N. Permission proof: PermissionNames still contains exactly five Photography.* permissions; no recovery permission.
    [Fact]
    public void N_photography_permissions_remain_exactly_five_with_no_recovery_permission()
    {
        var photographyPermissions = PermissionNames.All
            .Where(permission => permission.StartsWith("Photography.", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(5, photographyPermissions.Length);
        Assert.Equal([
            PermissionNames.PhotographyView,
            PermissionNames.PhotographyUpload,
            PermissionNames.PhotographyManage,
            PermissionNames.PhotographyRequest,
            PermissionNames.PhotographyDelete
        ], photographyPermissions);
        Assert.DoesNotContain(photographyPermissions, permission => permission.Contains("Recovery", StringComparison.OrdinalIgnoreCase));
    }

    // O. Constructor proof: StorageOperationRecoveryUseCase has no permission/authorization dependency.
    [Fact]
    public void O_constructor_has_no_permission_or_authorization_dependency()
    {
        var parameterTypeNames = typeof(StorageOperationRecoveryUseCase)
            .GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("Permission", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("Authoriz", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            ["IMuseumDbContext", "IArtifactImageStorage", "ArtifactImageDeletionFinalizationService", "IAuditWriter", "TimeProvider"],
            parameterTypeNames);
    }

    // P. Unsupported recovery type: no speculative storage call; FailedNeedsAttention; safe outcome/audit.
    [Theory]
    [InlineData(StorageOperationRecoveryType.DerivativeCleanup)]
    [InlineData(StorageOperationRecoveryType.MissingObject)]
    [InlineData(StorageOperationRecoveryType.DerivativeGeneration)]
    public async Task P_unsupported_recovery_type_skips_storage_and_marks_failed_needs_attention(StorageOperationRecoveryType operationType)
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("unsupported");
        var recovery = StorageOperationRecovery.Create(operationType, artifact.ArtifactId, [key], "Unsupported recovery scenario.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, storage) = NewUseCase(RetryAt, db);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.UnsupportedRecoveryType, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Empty(storage.StatCalls);
        Assert.Empty(storage.DeleteObjectCalls);
        var persisted = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.FailedNeedsAttention, persisted.Status);
        var actions = (await db.AuditEntries.ToListAsync()).Select(entry => entry.ActionName).ToArray();
        Assert.Contains(PhotographyAuditActions.StorageConsistencyIssue, actions);
        Assert.DoesNotContain(PhotographyAuditActions.StorageRecoveryRetry, actions);
    }

    // Q. DeleteCleanup CASE A: Stat finds remaining object; delete it; then finalizes DeletePending image.
    [Fact]
    public async Task Q_delete_cleanup_case_a_deletes_remaining_object_then_finalizes()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "Storage objects were deleted but final deletion metadata could not be committed.", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.SeedExisting(image.OriginalObjectKey);
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        Assert.Contains(image.OriginalObjectKey, storage.StatCalls);
        Assert.Contains(image.OriginalObjectKey, storage.DeleteObjectCalls);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, (await db.StorageOperationRecoveries.SingleAsync()).Status);
    }

    // R. DeleteCleanup CASE B: Stat returns NotFound for objects already deleted; NO DeleteObjectAsync call; metadata finalization completes.
    [Fact]
    public async Task R_delete_cleanup_case_b_skips_delete_call_when_already_absent_then_finalizes()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "Storage objects were deleted but final deletion metadata could not be committed.", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        Assert.Contains(image.OriginalObjectKey, storage.StatCalls);
        Assert.Empty(storage.DeleteObjectCalls);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
    }

    // S. DeleteCleanup restart finalization uses durable original deletion attribution.
    [Fact]
    public async Task S_finalization_audit_uses_original_deletion_requester_not_recovery_worker()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db, requestedByUserId: "photographer-1");
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, _) = NewUseCase(RetryAt, db, actorUserId: "recovery-worker");

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        var deletionAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace);
        Assert.Equal("photographer-1", deletionAudit.ActorUserId);
        Assert.NotEqual("recovery-worker", deletionAudit.ActorUserId);
    }

    // T. Privileged DeleteCleanup: reason retained; business actor retained.
    [Fact]
    public async Task T_privileged_delete_cleanup_retains_reason_and_business_actor()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(
            db, mode: ArtifactImageDeletionMode.Privileged, requestedByUserId: "supervisor-1", reason: "duplicate accession photo");
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var (useCase, _) = NewUseCase(RetryAt, db, actorUserId: "recovery-worker");

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal("duplicate accession photo", finalImage.DeletionReason);
        var deletionAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged);
        Assert.Equal("supervisor-1", deletionAudit.ActorUserId);
        Assert.Contains("Reason=duplicate accession photo", deletionAudit.ChangeSummary);
    }

    // U. Legacy DeletePending missing deletion-intent attribution: FailedNeedsAttention; no finalization; no guessed actor.
    [Fact]
    public async Task U_legacy_delete_pending_without_attribution_marks_failed_needs_attention_without_finalizing()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        ForceLegacyDeletePendingWithoutIntent(image, ArtifactImageDeletionMode.UploaderGracePeriod);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.InvalidState, result.Outcome);
        Assert.False(result.Succeeded);
        var unchangedImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, unchangedImage.Status);
        Assert.Null(unchangedImage.DeletedAt);
        Assert.Equal(0, await db.AuditEntries.CountAsync(entry =>
            entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace || entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged));
        Assert.Equal(StorageOperationRecoveryStatus.FailedNeedsAttention, (await db.StorageOperationRecoveries.SingleAsync()).Status);
    }

    // V. Already Deleted image + verified absent objects: recovery resolves idempotently; no duplicate deletion audit.
    [Fact]
    public async Task V_already_deleted_image_with_absent_objects_resolves_idempotently()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletionRequestedAt);
        image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "stale recovery row", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        Assert.Equal(0, await db.AuditEntries.CountAsync(entry =>
            entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace || entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged));
    }

    // W. Duplicate DeleteCleanup rows: successful finalization may resolve all; rows retained; safe already-resolved reload.
    [Fact]
    public async Task W_duplicate_delete_cleanup_rows_all_resolve_and_are_retained()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db);
        var firstRecovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "first attempt", image.ArtifactImageId);
        var secondRecovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "second attempt", image.ArtifactImageId);
        db.StorageOperationRecoveries.AddRange(firstRecovery, secondRecovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var firstResult = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(firstRecovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, firstResult.Outcome);
        var rows = await db.StorageOperationRecoveries.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(StorageOperationRecoveryStatus.Resolved, row.Status));

        var secondResult = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(secondRecovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.AlreadyResolved, secondResult.Outcome);
        Assert.Equal(2, await db.StorageOperationRecoveries.CountAsync());
    }

    // X. Primary Image remains null; no replacement.
    [Fact]
    public async Task X_primary_image_stays_null_after_recovery_finalization()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        db.ArtifactPhotographyStates.Add(state);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        var finalState = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Null(finalState.PrimaryImageId);
    }

    // Y. Artifact custody/current holder/current location/movement remain unchanged.
    [Fact]
    public async Task Y_custody_movement_and_location_remain_unchanged()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        var beforeStatus = artifact.CurrentStatus;
        var beforeHolderType = artifact.CurrentHolderType;
        var beforeHolderName = artifact.CurrentHolderName;
        var beforeLocationId = artifact.CurrentLocationId;
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var movementCountBefore = await db.MovementRecords.CountAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        var artifactAfter = await db.Artifacts.SingleAsync();
        Assert.Equal(beforeStatus, artifactAfter.CurrentStatus);
        Assert.Equal(beforeHolderType, artifactAfter.CurrentHolderType);
        Assert.Equal(beforeHolderName, artifactAfter.CurrentHolderName);
        Assert.Equal(beforeLocationId, artifactAfter.CurrentLocationId);
        Assert.Equal(movementCountBefore, await db.MovementRecords.CountAsync());
    }

    // Z1. Resolution timestamp defect proof: DeletionRequestedAt != RetryAt; recovery ResolvedAt/LastAttemptedAt
    // use the actual recovery/finalization time, not the original deletion-intent time; DeletedAt is unchanged;
    // and the StorageRecoveryResolved audit reports the real Retrying -> Resolved transition (not Resolved -> Resolved).
    [Fact]
    public async Task Z1_recovery_resolution_time_uses_actual_finalization_time_not_original_deletion_intent_time()
    {
        Assert.NotEqual(DeletionRequestedAt, RetryAt);
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db, requestedAt: DeletionRequestedAt);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, result.Outcome);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(DeletionRequestedAt, finalImage.DeletedAt);
        Assert.Equal(DeletionRequestedAt, finalImage.DeletionRequestedAt);

        var resolvedRecovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, resolvedRecovery.Status);
        Assert.Equal(RetryAt, resolvedRecovery.ResolvedAt);
        Assert.Equal(RetryAt, resolvedRecovery.LastAttemptedAt);
        Assert.NotEqual(DeletionRequestedAt, resolvedRecovery.ResolvedAt);

        var resolvedAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.StorageRecoveryResolved);
        Assert.Contains("PreviousStatus=Retrying", resolvedAudit.ChangeSummary);
        Assert.Contains("NewStatus=Resolved", resolvedAudit.ChangeSummary);
        Assert.Contains($"AttemptedAtUtc={RetryAt:O}", resolvedAudit.ChangeSummary);
        Assert.Contains($"ResolvedAtUtc={RetryAt:O}", resolvedAudit.ChangeSummary);
        Assert.DoesNotContain("Resolved -> Resolved", resolvedAudit.ChangeSummary);
    }

    // Z2. Duplicate DeleteCleanup rows: every row resolved by finalization receives the actual finalization
    // time, never the original deletion-intent time.
    [Fact]
    public async Task Z2_duplicate_delete_cleanup_rows_all_receive_actual_finalization_time()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db, requestedAt: DeletionRequestedAt);
        var firstRecovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "first attempt", image.ArtifactImageId);
        var secondRecovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "second attempt", image.ArtifactImageId);
        db.StorageOperationRecoveries.AddRange(firstRecovery, secondRecovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(firstRecovery.StorageOperationRecoveryId));

        var rows = await db.StorageOperationRecoveries.ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(StorageOperationRecoveryStatus.Resolved, row.Status);
            Assert.Equal(RetryAt, row.ResolvedAt);
            Assert.NotEqual(DeletionRequestedAt, row.ResolvedAt);
        });
    }

    // Z3. UploadCleanup DeleteObjectAsync throws an unexpected exception carrying deliberately sensitive-looking
    // text (bucket/endpoint/object key/server path); the failure is fully contained.
    [Fact]
    public async Task Z3_upload_cleanup_unexpected_delete_exception_is_contained_and_safe()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("boom");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        const string sensitiveMessage = "Connection to bucket 'museum-prod-bucket' at endpoint https://storage.internal:9000 failed for object key artifact-images/abc/original.jpg under path /srv/minio/data";
        storage.ThrowOnDelete(key, new InvalidOperationException(sensitiveMessage));
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.RetryFailed, result.Outcome);
        Assert.False(result.Succeeded);
        AssertNoSensitiveLeak(result.StaffFacingMessage);
        var persisted = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.FailedNeedsAttention, persisted.Status);
        AssertNoSensitiveLeak(persisted.FailureSummary);
        var consistencyAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.StorageConsistencyIssue);
        AssertNoSensitiveLeak(consistencyAudit.Summary);
        AssertNoSensitiveLeak(consistencyAudit.ChangeSummary ?? string.Empty);
    }

    // Z4. DeleteCleanup StatAsync throws an unexpected exception; same safe controlled behavior.
    [Fact]
    public async Task Z4_delete_cleanup_unexpected_stat_exception_is_contained_and_safe()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        const string sensitiveMessage = "provider endpoint https://storage.internal:9000 unreachable for bucket private-artifacts";
        storage.ThrowOnStat(image.OriginalObjectKey, new InvalidOperationException(sensitiveMessage));
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.RetryFailed, result.Outcome);
        Assert.False(result.Succeeded);
        AssertNoSensitiveLeak(result.StaffFacingMessage);
        var persisted = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.FailedNeedsAttention, persisted.Status);
        AssertNoSensitiveLeak(persisted.FailureSummary);
        Assert.Empty(storage.DeleteObjectCalls);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, finalImage.Status);
        var consistencyAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.StorageConsistencyIssue);
        AssertNoSensitiveLeak(consistencyAudit.ChangeSummary ?? string.Empty);
    }

    // Z5. DeleteCleanup DeleteObjectAsync throws an unexpected exception; same safe controlled behavior.
    [Fact]
    public async Task Z5_delete_cleanup_unexpected_delete_exception_is_contained_and_safe()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = await SeedDeletePendingImageAsync(db);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup, artifact.ArtifactId, [image.OriginalObjectKey], "summary", image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.SeedExisting(image.OriginalObjectKey);
        const string sensitiveMessage = "credential AKIAEXAMPLE rejected for object under server path /srv/minio/data/private-artifacts";
        storage.ThrowOnDelete(image.OriginalObjectKey, new InvalidOperationException(sensitiveMessage));
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        var result = await useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.RetryFailed, result.Outcome);
        Assert.False(result.Succeeded);
        AssertNoSensitiveLeak(result.StaffFacingMessage);
        var persisted = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.FailedNeedsAttention, persisted.Status);
        AssertNoSensitiveLeak(persisted.FailureSummary);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, finalImage.Status);
    }

    // Z6. OperationCanceledException is rethrown, not converted into FailedNeedsAttention; a recovery left
    // Retrying after cancellation remains retryable on the next attempt.
    [Fact]
    public async Task Z6_operation_canceled_exception_is_rethrown_and_recovery_remains_retryable()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var key = Key("cancel-me");
        var recovery = StorageOperationRecovery.Create(StorageOperationRecoveryType.UploadCleanup, artifact.ArtifactId, [key], "Storage cleanup could not be completed.");
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var storage = new RecoveryFakeStorage();
        storage.ThrowOnDelete(key, new OperationCanceledException("cancelled"));
        var (useCase, _) = NewUseCase(RetryAt, db, storage: storage);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            useCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId)));

        var persisted = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.Retrying, persisted.Status);
        Assert.NotEqual(StorageOperationRecoveryStatus.FailedNeedsAttention, persisted.Status);

        var recoveredStorage = new RecoveryFakeStorage();
        var (retryUseCase, _) = NewUseCase(RetryAt.AddMinutes(5), db, storage: recoveredStorage);
        var retryResult = await retryUseCase.RetryAsync(new StorageOperationRecoveryRetryCommand(recovery.StorageOperationRecoveryId));

        Assert.Equal(StorageOperationRecoveryRetryOutcome.Resolved, retryResult.Outcome);
    }

    private static void AssertNoSensitiveLeak(string text)
    {
        string[] sensitiveFragments = ["museum-prod-bucket", "storage.internal", "srv/minio", "private-artifacts", "AKIAEXAMPLE", "InvalidOperationException", "artifact-images/abc"];
        foreach (var fragment in sensitiveFragments)
        {
            Assert.DoesNotContain(fragment, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static ImageStorageObjectKey Key(string suffix) =>
        ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/{suffix}");

    private static void AssertSafeMessage(string message)
    {
        Assert.False(string.IsNullOrWhiteSpace(message));
        AssertSafeText(message);
    }

    private static void AssertSafeText(string text)
    {
        string[] forbidden = ["ObjectKey", "artifact-images/", "bucket", "endpoint", "minio", "presigned", "credential", "Exception"];
        foreach (var fragment in forbidden)
        {
            Assert.DoesNotContain(fragment, text, StringComparison.OrdinalIgnoreCase);
        }
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

    private static void ForceLegacyDeletePendingWithoutIntent(ArtifactImage image, ArtifactImageDeletionMode mode)
    {
        SetProperty(image, nameof(ArtifactImage.Status), ArtifactImageStatus.DeletePending);
        SetProperty(image, nameof(ArtifactImage.DeletionMode), mode);
    }

    private static void SetProperty<T>(ArtifactImage image, string propertyName, T value)
    {
        var property = typeof(ArtifactImage).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!;
        property.SetValue(image, value);
    }

    private static async Task<(Artifact Artifact, PhotographySet Set, ArtifactImage Image)> SeedDeletePendingImageAsync(
        MuseumDbContext db,
        ArtifactImageDeletionMode mode = ArtifactImageDeletionMode.UploaderGracePeriod,
        string requestedByUserId = "photographer-1",
        DateTimeOffset? requestedAt = null,
        string? reason = null)
    {
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        image.MarkDeletePending(mode, requestedByUserId, requestedAt ?? DeletionRequestedAt, reason);
        await db.SaveChangesAsync();
        return (artifact, set, image);
    }

    private static (StorageOperationRecoveryUseCase UseCase, RecoveryFakeStorage Storage) NewUseCase(
        DateTimeOffset now,
        MuseumDbContext db,
        IMuseumDbContext? persistenceContext = null,
        RecoveryFakeStorage? storage = null,
        string actorUserId = "recovery-worker")
    {
        var context = persistenceContext ?? db;
        storage ??= new RecoveryFakeStorage();
        var actorContext = new TestAuditActorContext(actorUserId);
        var auditWriter = new AuditWriter(context, actorContext);
        var clock = new FixedTimeProvider(now);
        var finalizationService = new ArtifactImageDeletionFinalizationService(context, auditWriter, clock);
        var useCase = new StorageOperationRecoveryUseCase(context, storage, finalizationService, auditWriter, clock);
        return (useCase, storage);
    }
}

internal sealed class RecoveryFakeStorage : IArtifactImageStorage
{
    private readonly HashSet<string> existingKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArtifactImageStorageResultKind> deleteOverrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> statThrows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Exception> deleteThrows = new(StringComparer.Ordinal);

    public List<ImageStorageObjectKey> StatCalls { get; } = [];
    public List<ImageStorageObjectKey> DeleteObjectCalls { get; } = [];

    public void SeedExisting(ImageStorageObjectKey key) => existingKeys.Add(key.Value);

    public void OverrideDelete(ImageStorageObjectKey key, ArtifactImageStorageResultKind kind) => deleteOverrides[key.Value] = kind;

    public void ThrowOnStat(ImageStorageObjectKey key, Exception exception) => statThrows[key.Value] = exception;

    public void ThrowOnDelete(ImageStorageObjectKey key, Exception exception) => deleteThrows[key.Value] = exception;

    public ValueTask<ArtifactImageStorageStatResult> StatAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default)
    {
        StatCalls.Add(objectKey);
        if (statThrows.TryGetValue(objectKey.Value, out var exception))
        {
            throw exception;
        }

        return ValueTask.FromResult(existingKeys.Contains(objectKey.Value)
            ? ArtifactImageStorageStatResult.Success(Metadata(objectKey))
            : ArtifactImageStorageStatResult.Failed(ArtifactImageStorageResultKind.NotFound, "NotFound", "Stored object was not found."));
    }

    public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default)
    {
        DeleteObjectCalls.Add(objectKey);
        if (deleteThrows.TryGetValue(objectKey.Value, out var exception))
        {
            throw exception;
        }

        existingKeys.Remove(objectKey.Value);

        if (deleteOverrides.TryGetValue(objectKey.Value, out var kind) && kind != ArtifactImageStorageResultKind.Success)
        {
            return ValueTask.FromResult(ArtifactImageStorageDeleteResult.Failed(objectKey, kind, "Simulated", "Image storage is currently unavailable."));
        }

        return ValueTask.FromResult(ArtifactImageStorageDeleteResult.Success(objectKey));
    }

    private static ArtifactImageStoredObjectMetadata Metadata(ImageStorageObjectKey objectKey) =>
        new(objectKey, "image/jpeg", 128, null, DateTimeOffset.UtcNow);

    public ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, string? checksum, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, ImageDerivativeKind derivativeKind, string? checksum, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(ImageStorageObjectKey objectKey, TimeSpan requestedLifetime, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class RecoveryConcurrencyFaultingDbContext(MuseumDbContext inner) : IMuseumDbContext
{
    public bool ThrowNextRecoveryConcurrency { get; set; }
    public int RecoveryConcurrencyFailuresThrown { get; private set; }
    public int ClearTrackedChangesCalls { get; private set; }

    public DbSet<ArtifactCategory> ArtifactCategories => inner.ArtifactCategories;
    public DbSet<Artifact> Artifacts => inner.Artifacts;
    public DbSet<Location> Locations => inner.Locations;
    public DbSet<MovementRecord> MovementRecords => inner.MovementRecords;
    public DbSet<ImportBatch> ImportBatches => inner.ImportBatches;
    public DbSet<ImportRow> ImportRows => inner.ImportRows;
    public DbSet<ReconciliationSession> ReconciliationSessions => inner.ReconciliationSessions;
    public DbSet<ReconciliationResult> ReconciliationResults => inner.ReconciliationResults;
    public DbSet<DocumentedCorrection> DocumentedCorrections => inner.DocumentedCorrections;
    public DbSet<AuditEntry> AuditEntries => inner.AuditEntries;
    public DbSet<DocumentationTemplate> DocumentationTemplates => inner.DocumentationTemplates;
    public DbSet<DocumentationTemplateVersion> DocumentationTemplateVersions => inner.DocumentationTemplateVersions;
    public DbSet<DocumentationTemplateField> DocumentationTemplateFields => inner.DocumentationTemplateFields;
    public DbSet<DocumentationTemplateFieldOption> DocumentationTemplateFieldOptions => inner.DocumentationTemplateFieldOptions;
    public DbSet<DocumentationRecord> DocumentationRecords => inner.DocumentationRecords;
    public DbSet<DocumentationRevision> DocumentationRevisions => inner.DocumentationRevisions;
    public DbSet<PhotographyRequest> PhotographyRequests => inner.PhotographyRequests;
    public DbSet<PhotographySet> PhotographySets => inner.PhotographySets;
    public DbSet<ArtifactImage> ArtifactImages => inner.ArtifactImages;
    public DbSet<ArtifactImageDerivative> ArtifactImageDerivatives => inner.ArtifactImageDerivatives;
    public DbSet<ArtifactPhotographyState> ArtifactPhotographyStates => inner.ArtifactPhotographyStates;
    public DbSet<PhotographyUploadOperation> PhotographyUploadOperations => inner.PhotographyUploadOperations;
    public DbSet<PhotographyUploadFileOutcome> PhotographyUploadFileOutcomes => inner.PhotographyUploadFileOutcomes;
    public DbSet<StorageOperationRecovery> StorageOperationRecoveries => inner.StorageOperationRecoveries;

    public Task<IMuseumDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.BeginTransactionAsync(cancellationToken);

    public void ClearTrackedChanges()
    {
        ClearTrackedChangesCalls++;
        inner.ClearTrackedChanges();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowNextRecoveryConcurrency && inner.ChangeTracker.Entries<StorageOperationRecovery>().Any(entry => entry.State == EntityState.Modified))
        {
            ThrowNextRecoveryConcurrency = false;
            RecoveryConcurrencyFailuresThrown++;
            throw new DbUpdateConcurrencyException("Simulated storage operation recovery concurrency failure.");
        }

        return inner.SaveChangesAsync(cancellationToken);
    }
}

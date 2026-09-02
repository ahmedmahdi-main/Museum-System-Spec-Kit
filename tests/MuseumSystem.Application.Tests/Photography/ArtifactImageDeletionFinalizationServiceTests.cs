using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class ArtifactImageDeletionFinalizationServiceTests
{
    private static readonly DateTimeOffset DeletedAt = new(2026, 8, 25, 12, 5, 0, TimeSpan.Zero);

    [Fact]
    public async Task Delete_pending_grace_image_finalizes_to_deleted_with_captured_metadata()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt, image.ConcurrencyToken));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, result.Outcome);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
        Assert.Equal(ArtifactImageDeletionMode.UploaderGracePeriod, finalImage.DeletionMode);
        Assert.Equal("photographer-1", finalImage.DeletedByUserId);
        Assert.Equal(DeletedAt, finalImage.DeletedAt);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.ImageDeleteByUploaderGrace, audit.ActionName);
        Assert.Contains("Rule=UploaderGracePeriod", audit.ChangeSummary);
    }

    [Fact]
    public async Task Delete_pending_privileged_image_finalizes_and_audit_retains_reason()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.Privileged, "duplicate accession photo");
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.Privileged, "supervisor-1", DeletedAt, image.ConcurrencyToken));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, result.Outcome);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal("duplicate accession photo", finalImage.DeletionReason);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.ImageDeletePrivileged, audit.ActionName);
        Assert.Contains("Reason=duplicate accession photo", audit.ChangeSummary);
        Assert.DoesNotContain("ObjectKey", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Already_deleted_image_finalizes_idempotently_without_duplicate_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt.AddMinutes(5)));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.AlreadyFinalized, result.Outcome);
        Assert.True(result.Succeeded);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(DeletedAt, finalImage.DeletedAt);
    }

    [Fact]
    public async Task Available_image_cannot_be_finalized()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.InvalidState, result.Outcome);
        var unchanged = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Available, unchanged.Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Mismatched_pending_mode_is_rejected_as_invalid_state()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.Privileged, "supervisor-1", DeletedAt));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.InvalidState, result.Outcome);
        var unchanged = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, unchanged.Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Mismatched_expected_concurrency_token_conflicts_without_mutation()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt, image.ConcurrencyToken + 1));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Conflict, result.Outcome);
        var unchanged = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, unchanged.Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Final_save_changes_concurrency_failure_leaves_image_delete_pending_and_returns_finalization_pending()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        await db.SaveChangesAsync();
        var faulting = new FaultingPhotographyManagementDbContext(db) { ThrowNextImageConcurrency = true };
        var service = NewService(db, persistenceContext: faulting);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt, image.ConcurrencyToken));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.FinalizationPending, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Equal(1, faulting.ImageConcurrencyFailuresThrown);
        var unchanged = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, unchanged.Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Audit_write_failure_returns_finalization_pending_and_writes_no_audit_entry()
    {
        // The in-memory EF provider has no real transaction to roll back (MuseumDbContext.BeginTransactionAsync
        // returns a no-op transaction for non-relational providers), so this proves the outcome/audit contract;
        // true atomic rollback of the Deleted transition is guaranteed by the real PostgreSQL transaction used
        // in production and is exercised by the PostgreSQL-backed persistence tests.
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        await db.SaveChangesAsync();
        var faultingAuditWriter = new ThrowingAuditWriter();
        var service = new ArtifactImageDeletionFinalizationService(db, faultingAuditWriter);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt, image.ConcurrencyToken));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.FinalizationPending, result.Outcome);
        Assert.False(result.Succeeded);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Storage_succeeded_but_finalization_failed_records_a_best_effort_recovery_row()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        await db.SaveChangesAsync();
        var faultingAuditWriter = new ThrowingAuditWriter();
        var service = new ArtifactImageDeletionFinalizationService(db, faultingAuditWriter);

        await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt, image.ConcurrencyToken));

        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryType.DeleteCleanup, recovery.OperationType);
        Assert.Equal(image.ArtifactImageId, recovery.ArtifactImageId);
        Assert.Equal(StorageOperationRecoveryStatus.Pending, recovery.Status);
    }

    [Fact]
    public async Task Resolved_delete_cleanup_recovery_is_retained_not_deleted_after_finalization_succeeds()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = SeedDeletePendingImage(db, ArtifactImageDeletionMode.UploaderGracePeriod);
        var recovery = StorageOperationRecovery.Create(
            StorageOperationRecoveryType.DeleteCleanup,
            artifact.ArtifactId,
            [image.OriginalObjectKey],
            "Storage cleanup failed before this test finalized the deletion.",
            image.ArtifactImageId);
        db.StorageOperationRecoveries.Add(recovery);
        await db.SaveChangesAsync();
        var service = NewService(db);

        var result = await service.FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(
            image.ArtifactImageId, ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", DeletedAt, image.ConcurrencyToken));

        Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, result.Outcome);
        var storedRecovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryStatus.Resolved, storedRecovery.Status);
        Assert.NotNull(storedRecovery.ResolvedAt);
    }

    [Fact]
    public void Finalization_service_never_depends_on_image_storage()
    {
        var parameterTypes = typeof(ArtifactImageDeletionFinalizationService)
            .GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(parameterTypes, name => name.Contains("Storage", StringComparison.Ordinal));
    }

    private static ArtifactImageDeletionFinalizationService NewService(MuseumDbContext db, IMuseumDbContext? persistenceContext = null)
    {
        var context = persistenceContext ?? db;
        var actorContext = new TestAuditActorContext("photographer-1");
        var auditWriter = new AuditWriter(context, actorContext);
        return new ArtifactImageDeletionFinalizationService(context, auditWriter);
    }

    private static (Artifact Artifact, PhotographySet Set, ArtifactImage Image) SeedDeletePendingImage(
        MuseumDbContext db,
        ArtifactImageDeletionMode mode,
        string? reason = null)
    {
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        image.MarkDeletePending(mode, reason);
        return (artifact, set, image);
    }
}

internal sealed class ThrowingAuditWriter : IAuditWriter
{
    public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default) =>
        throw new DbUpdateException("Simulated audit write failure.");
}

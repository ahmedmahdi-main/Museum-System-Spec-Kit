using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class DeleteArtifactImagePrivilegedUseCaseTests
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Photography_delete_permission_succeeds_with_a_valid_reason()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactImageDeletionMode.Privileged, result.Value!.DeletionMode);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
        Assert.Equal("supervisor-1", finalImage.DeletionRequestedByUserId);
        Assert.Equal(UploadedAt.AddMinutes(5), finalImage.DeletionRequestedAt);
        Assert.Equal(finalImage.DeletionRequestedByUserId, finalImage.DeletedByUserId);
        Assert.Equal(finalImage.DeletionRequestedAt, finalImage.DeletedAt);
        Assert.Single(host.Storage.DeleteImageObjectCalls);
    }

    [Theory]
    [InlineData(PermissionNames.PhotographyView)]
    [InlineData(PermissionNames.PhotographyUpload)]
    [InlineData(PermissionNames.PhotographyManage)]
    [InlineData(PermissionNames.PhotographyRequest)]
    public async Task Other_photography_permissions_alone_do_not_authorize_privileged_deletion(string permission)
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5), permissions: [permission]);

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "reason", image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Delete_permission_without_upload_permission_still_succeeds()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, actorUserId: "supervisor-1", now: UploadedAt.AddMinutes(5), permissions: [PermissionNames.PhotographyDelete]);

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Supervisor_may_delete_another_users_image()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "original-uploader", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, actorUserId: "supervisor-1", now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal("supervisor-1", (await db.ArtifactImages.SingleAsync()).DeletedByUserId);
    }

    [Fact]
    public async Task Privileged_deletion_is_allowed_inside_the_uploader_grace_window()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "original-uploader", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, actorUserId: "supervisor-1", now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Privileged_deletion_is_allowed_after_the_grace_window_expires()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "original-uploader", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, actorUserId: "supervisor-1", now: UploadedAt.AddMinutes(120));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_reason_is_rejected_without_calling_storage_or_writing_audit(string? reason)
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, reason, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionReasonRequired");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
        Assert.Equal(ArtifactImageStatus.Available, (await db.ArtifactImages.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Reason_longer_than_the_stored_maximum_is_rejected_before_persistence()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, new string('x', 1001), image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionReasonTooLong");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Reason_is_normalized_by_trimming_surrounding_whitespace()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "  duplicate accession photograph  ", image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal("duplicate accession photograph", (await db.ArtifactImages.SingleAsync()).DeletionReason);
        var audit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged);
        Assert.Contains("Reason=duplicate accession photograph", audit.ChangeSummary);
    }

    [Fact]
    public async Task Privileged_audit_identifies_action_identity_actor_timestamp_and_reason_without_storage_internals()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var deletedAt = UploadedAt.AddMinutes(5);
        var host = NewHost(db, actorUserId: "supervisor-1", now: deletedAt);

        await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        var audit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged);
        Assert.Equal(image.ArtifactImageId.ToString(), audit.EntityId);
        Assert.Contains($"ArtifactId={artifact.ArtifactId}", audit.ChangeSummary);
        Assert.Contains("ActorUserId=supervisor-1", audit.ChangeSummary);
        Assert.Contains($"DeletedAtUtc={deletedAt:O}", audit.ChangeSummary);
        Assert.Contains("Reason=duplicate accession photo", audit.ChangeSummary);
        Assert.DoesNotContain("ObjectKey", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deleting_current_primary_clears_it_and_writes_both_primary_and_privileged_deletion_audits()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = SeedImage(db, artifact, set, "photographer-1", UploadedAt);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var finalState = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Null(finalState.PrimaryImageId);
        var audits = await db.AuditEntries.ToListAsync();
        Assert.Contains(audits, entry => entry.ActionName == PhotographyAuditActions.PrimaryImageChange);
        Assert.Contains(audits, entry => entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged);
        Assert.Equal(2, audits.Count);
    }

    [Fact]
    public async Task Stale_expected_token_conflicts_without_calling_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "reason", image.ConcurrencyToken + 1));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
    }

    [Theory]
    [InlineData(ArtifactImageStatus.DeletePending)]
    [InlineData(ArtifactImageStatus.Deleted)]
    public async Task Non_available_image_returns_invalid_state_without_invoking_internal_finalization(ArtifactImageStatus status)
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = SeedImage(db, artifact, set, "photographer-1", UploadedAt);
        image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "supervisor-1", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero), "earlier reason");
        if (status == ArtifactImageStatus.Deleted)
        {
            image.MarkDeleted(ArtifactImageDeletionMode.Privileged);
        }

        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(20));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "new reason", image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeleteInvalidState");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Storage_partial_failure_is_reported_as_recovery_required_not_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5));
        host.Storage.QueueDeleteResult(ArtifactImageObjectsDeleteResult.PartialFailure(
            [ArtifactImageStorageDeleteResult.Failed(image.OriginalObjectKey, ArtifactImageStorageResultKind.RetryableFailure, "Storage.RetryableFailure", "Image storage is currently unavailable.")],
            "Storage.DeletePartialFailure",
            "One or more stored image objects could not be deleted.",
            "provider://internal/delete"));

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionRecoveryRequired");
        Assert.DoesNotContain(result.ValidationIssues, issue => issue.Message.Contains("provider://", StringComparison.OrdinalIgnoreCase));
        var pendingImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, pendingImage.Status);
        Assert.Equal("supervisor-1", pendingImage.DeletionRequestedByUserId);
        Assert.Equal(UploadedAt.AddMinutes(5), pendingImage.DeletionRequestedAt);
        Assert.Null(pendingImage.DeletedByUserId);
        Assert.Null(pendingImage.DeletedAt);
    }

    [Fact]
    public async Task Finalization_pending_after_storage_success_is_reported_as_not_successful()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(5), auditWriter: new ThrowingAuditWriter());

        var result = await host.UseCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(
            image.ArtifactImageId, "duplicate accession photo", image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionFinalizationPending");
        Assert.Single(host.Storage.DeleteImageObjectCalls);
    }

    [Fact]
    public void Command_carries_only_image_identity_reason_and_concurrency_token()
    {
        var memberNames = typeof(DeleteArtifactImagePrivilegedCommand)
            .GetConstructors().Single().GetParameters().Select(parameter => parameter.Name ?? string.Empty)
            .Concat(typeof(DeleteArtifactImagePrivilegedCommand).GetProperties().Select(property => property.Name))
            .ToArray();

        Assert.Equal(["ArtifactImageId", "DeletionReason", "ExpectedConcurrencyToken"], memberNames.Distinct().Order());

        var forbidden = new[] { "Actor", "UserId", "Time", "DeletedAt", "UploadedAt", "Permission", "ObjectKey", "Bucket", "Storage" };
        foreach (var fragment in forbidden)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Use_case_constructor_does_not_depend_on_image_storage()
    {
        var parameterTypeNames = typeof(DeleteArtifactImagePrivilegedUseCase)
            .GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("Storage", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("AuditWriter", StringComparison.Ordinal));
        Assert.Contains(parameterTypeNames, name => name == nameof(ArtifactImageDeletionService));
    }

    private static DeleteArtifactImagePrivilegedHost NewHost(
        MuseumDbContext db,
        string actorUserId = "supervisor-1",
        DateTimeOffset? now = null,
        IReadOnlyCollection<string>? permissions = null,
        IAuditWriter? auditWriter = null)
    {
        var actorContext = new TestAuditActorContext(actorUserId);
        var writer = auditWriter ?? new AuditWriter(db, actorContext);
        var storage = new ScriptedArtifactImageStorage();
        var finalizationService = new ArtifactImageDeletionFinalizationService(db, writer);
        var deletionService = new ArtifactImageDeletionService(db, writer, storage, finalizationService);
        var permissionChecker = new FakeCurrentActorPermissionChecker(permissions ?? [PermissionNames.PhotographyDelete]);
        var clock = new FixedTimeProvider(now ?? UploadedAt);
        var useCase = new DeleteArtifactImagePrivilegedUseCase(db, actorContext, permissionChecker, clock, deletionService);
        return new DeleteArtifactImagePrivilegedHost(useCase, storage);
    }

    private static (Artifact Artifact, PhotographySet Set, ArtifactImage Image) SeedAvailableImage(
        MuseumDbContext db, string uploadedByUserId, DateTimeOffset uploadedAt)
    {
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = SeedImage(db, artifact, set, uploadedByUserId, uploadedAt);
        return (artifact, set, image);
    }

    private static ArtifactImage SeedImage(
        MuseumDbContext db, Artifact artifact, PhotographySet set, string uploadedByUserId, DateTimeOffset uploadedAt)
    {
        var image = ArtifactImage.Create(
            artifact.ArtifactId,
            set.PhotographySetId,
            ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/original.jpg"),
            "front.jpg",
            "image/jpeg",
            128,
            800,
            600,
            uploadedByUserId,
            uploadedAt);
        db.ArtifactImages.Add(image);
        return image;
    }
}

internal sealed record DeleteArtifactImagePrivilegedHost(
    DeleteArtifactImagePrivilegedUseCase UseCase,
    ScriptedArtifactImageStorage Storage);

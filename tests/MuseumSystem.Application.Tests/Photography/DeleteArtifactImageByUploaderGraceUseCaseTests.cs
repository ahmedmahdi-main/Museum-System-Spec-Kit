using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class DeleteArtifactImageByUploaderGraceUseCaseTests
{
    private static readonly DateTimeOffset UploadedAt = new(2026, 8, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Fifty_nine_minutes_after_upload_succeeds_and_finalizes_with_grace_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(59));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactImageStatus.Deleted, result.Value!.Status);
        Assert.Equal(ArtifactImageDeletionMode.UploaderGracePeriod, result.Value.DeletionMode);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
        Assert.Null(finalImage.DeletionReason);
        var deletionAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace);
        Assert.Equal(image.ArtifactImageId.ToString(), deletionAudit.EntityId);
        Assert.DoesNotContain(await db.AuditEntries.ToListAsync(), entry => entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged);
        Assert.Single(host.Storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Exactly_sixty_minutes_after_upload_is_still_allowed()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(60));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal(ArtifactImageStatus.Deleted, (await db.ArtifactImages.SingleAsync()).Status);
    }

    [Fact]
    public async Task One_tick_past_sixty_minutes_is_denied_as_expired_without_touching_storage_or_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(60).AddTicks(1));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.GracePeriodExpired");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
        Assert.Equal(ArtifactImageStatus.Available, (await db.ArtifactImages.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Future_upload_time_relative_to_server_now_is_not_eligible()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddTicks(-1));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.GracePeriodExpired");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
        Assert.Equal(ArtifactImageStatus.Available, (await db.ArtifactImages.SingleAsync()).Status);
    }

    [Fact]
    public async Task Revoked_upload_permission_denies_grace_deletion_without_mutation_storage_or_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(30), permissions: []);

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
        Assert.Equal(ArtifactImageStatus.Available, (await db.ArtifactImages.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Theory]
    [InlineData(PermissionNames.PhotographyDelete)]
    [InlineData(PermissionNames.PhotographyManage)]
    [InlineData(PermissionNames.PhotographyView)]
    public async Task Other_photography_permissions_alone_do_not_authorize_grace_deletion(string permission)
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(30), permissions: [permission]);

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Another_users_image_is_rejected_as_uploader_mismatch_without_touching_storage_or_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "original-uploader", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, actorUserId: "another-photographer", now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.UploaderMismatch");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
        Assert.Equal(ArtifactImageStatus.Available, (await db.ArtifactImages.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Uploader_mismatch_failure_does_not_expose_the_original_uploaders_identity()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "original-uploader", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, actorUserId: "another-photographer", now: UploadedAt.AddMinutes(5));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.DoesNotContain(result.ValidationIssues, issue => issue.Message.Contains("original-uploader", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Messages, message => message.Contains("original-uploader", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_image_and_invalid_inputs_return_stable_failures()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var host = NewHost(db, now: UploadedAt);

        var emptyId = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(Guid.Empty, 0));
        var missing = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(Guid.NewGuid(), 0));
        var invalidToken = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(Guid.NewGuid(), -1));

        Assert.Contains(emptyId.ValidationIssues, issue => issue.Code == "ArtifactImage.Required");
        Assert.Contains(missing.ValidationIssues, issue => issue.Code == "ArtifactImage.NotFound");
        Assert.Contains(invalidToken.ValidationIssues, issue => issue.Code == "ArtifactImage.ConcurrencyTokenInvalid");
    }

    [Fact]
    public async Task Stale_expected_token_conflicts_without_calling_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(10));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken + 1));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
    }

    [Theory]
    [InlineData(ArtifactImageStatus.DeletePending)]
    [InlineData(ArtifactImageStatus.Deleted)]
    public async Task Non_available_image_returns_invalid_state_without_calling_storage(ArtifactImageStatus status)
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = SeedImage(db, artifact, set, "photographer-1", UploadedAt);
        image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
        if (status == ArtifactImageStatus.Deleted)
        {
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", UploadedAt.AddMinutes(10));
        }

        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(20));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeleteInvalidState");
        Assert.Empty(host.Storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Deleting_current_primary_clears_it_and_writes_both_primary_and_grace_deletion_audits()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = SeedImage(db, artifact, set, "photographer-1", UploadedAt);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(10));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var finalState = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Null(finalState.PrimaryImageId);
        var audits = await db.AuditEntries.ToListAsync();
        Assert.Contains(audits, entry => entry.ActionName == PhotographyAuditActions.PrimaryImageChange);
        Assert.Contains(audits, entry => entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace);
        Assert.Equal(2, audits.Count);
    }

    [Fact]
    public async Task Deleting_non_primary_image_writes_only_the_final_deletion_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var primary = SeedImage(db, artifact, set, "photographer-1", UploadedAt);
        var other = SeedImage(db, artifact, set, "photographer-1", UploadedAt);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(primary.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(10));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            other.ArtifactImageId, other.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var audits = await db.AuditEntries.ToListAsync();
        Assert.Single(audits);
        Assert.Equal(PhotographyAuditActions.ImageDeleteByUploaderGrace, audits[0].ActionName);
        Assert.Equal(primary.ArtifactImageId, (await db.ArtifactPhotographyStates.SingleAsync()).PrimaryImageId);
    }

    [Fact]
    public async Task Storage_partial_failure_is_reported_as_recovery_required_not_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(10));
        host.Storage.QueueDeleteResult(ArtifactImageObjectsDeleteResult.PartialFailure(
            [ArtifactImageStorageDeleteResult.Failed(image.OriginalObjectKey, ArtifactImageStorageResultKind.RetryableFailure, "Storage.RetryableFailure", "Image storage is currently unavailable.")],
            "Storage.DeletePartialFailure",
            "One or more stored image objects could not be deleted.",
            "provider://internal/delete"));

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionRecoveryRequired");
        Assert.DoesNotContain(result.ValidationIssues, issue => issue.Message.Contains("provider://", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.ValidationIssues, issue => issue.Message.Contains("Minio", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ArtifactImageStatus.DeletePending, (await db.ArtifactImages.SingleAsync()).Status);
    }

    [Fact]
    public async Task Finalization_pending_after_storage_success_is_reported_as_not_successful()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, "photographer-1", UploadedAt);
        await db.SaveChangesAsync();
        var host = NewHost(db, now: UploadedAt.AddMinutes(10), auditWriter: new ThrowingAuditWriter());

        var result = await host.UseCase.DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(
            image.ArtifactImageId, image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionFinalizationPending");
        Assert.Single(host.Storage.DeleteImageObjectCalls);
    }

    [Fact]
    public void Command_carries_only_image_identity_and_concurrency_token()
    {
        var memberNames = typeof(DeleteArtifactImageByUploaderGraceCommand)
            .GetConstructors().Single().GetParameters().Select(parameter => parameter.Name ?? string.Empty)
            .Concat(typeof(DeleteArtifactImageByUploaderGraceCommand).GetProperties().Select(property => property.Name))
            .ToArray();

        Assert.Equal(["ArtifactImageId", "ExpectedConcurrencyToken"], memberNames.Distinct().Order());

        var forbidden = new[] { "Actor", "UserId", "Time", "DeletedAt", "UploadedAt", "Permission", "ObjectKey", "Bucket", "Storage", "Reason" };
        foreach (var fragment in forbidden)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Deletion_dto_exposes_no_storage_internals()
    {
        var memberNames = typeof(ArtifactImageDeletionDto)
            .GetConstructors().Single().GetParameters().Select(parameter => parameter.Name ?? string.Empty)
            .Concat(typeof(ArtifactImageDeletionDto).GetProperties().Select(property => property.Name))
            .ToArray();

        var forbidden = new[] { "ObjectKey", "Bucket", "Endpoint", "Minio", "Presigned", "OperationalSummary", "FailureSummary", "DeletedByUserId" };
        foreach (var fragment in forbidden)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Use_case_constructor_does_not_depend_on_image_storage()
    {
        var parameterTypeNames = typeof(DeleteArtifactImageByUploaderGraceUseCase)
            .GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("Storage", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("AuditWriter", StringComparison.Ordinal));
        Assert.Contains(parameterTypeNames, name => name == nameof(ArtifactImageDeletionService));
    }

    private static DeleteArtifactImageByUploaderGraceHost NewHost(
        MuseumDbContext db,
        string actorUserId = "photographer-1",
        DateTimeOffset? now = null,
        IReadOnlyCollection<string>? permissions = null,
        IAuditWriter? auditWriter = null)
    {
        var actorContext = new TestAuditActorContext(actorUserId);
        var writer = auditWriter ?? new AuditWriter(db, actorContext);
        var storage = new ScriptedArtifactImageStorage();
        var finalizationService = new ArtifactImageDeletionFinalizationService(db, writer);
        var deletionService = new ArtifactImageDeletionService(db, writer, storage, finalizationService);
        var permissionChecker = new FakeCurrentActorPermissionChecker(permissions ?? [PermissionNames.PhotographyUpload]);
        var clock = new FixedTimeProvider(now ?? UploadedAt);
        var useCase = new DeleteArtifactImageByUploaderGraceUseCase(db, actorContext, permissionChecker, clock, deletionService);
        return new DeleteArtifactImageByUploaderGraceHost(useCase, storage);
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

internal sealed record DeleteArtifactImageByUploaderGraceHost(
    DeleteArtifactImageByUploaderGraceUseCase UseCase,
    ScriptedArtifactImageStorage Storage);

using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Contracts;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;
using MuseumSystem.Infrastructure.Photography.Imaging;
using MuseumSystem.Integration.Tests.Photography;

namespace MuseumSystem.Integration.Tests.Quickstart;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyQuickstartTests(PostgresPhotographyTestFixture postgres, MinioArtifactImageStorageTestFixture minio)
    : IClassFixture<MinioArtifactImageStorageTestFixture>, IAsyncLifetime
{
    private static readonly DateTimeOffset RequestClock = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PrimaryClock = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RecoveryClock = new(2026, 9, 1, 11, 0, 0, TimeSpan.Zero);
    private readonly List<Guid> createdArtifactIds = [];

    /*
     * T125 quickstart checklist map:
     * 1-2: Central_artifact_identity_and_feature001_custody_state_remain_authoritative_after_upload
     * 3-5: Multi_image_upload_returns_file_outcomes_and_persisted_idempotency_survives_restart
     * 6: Image_validation_accepts_jpeg_png_content_and_rejects_unsupported_or_spoofed_content
     * 7-8: Primary_image_state_has_one_authority_and_set_delete_concurrency_has_one_winner
     * 9-10: Request_completion_requires_upload_matching_fulfillment_and_explicit_per_request_completion
     * 11: Append_rejects_wrong_artifact_or_purpose_without_mutating_the_set
     * 12-13: Grace_and_privileged_deletion_use_actual_permission_reason_and_time_boundaries
     * 14: Permanent_deletion_removes_original_thumbnail_and_preview_objects
     * 15: Storage_cleanup_failure_leaves_recoverable_auditable_state_without_staff_storage_internals
     * 16: Deletion_finalization_failure_keeps_delete_pending_and_retry_finalizes_without_restoring_binary
     * 17: Staff_viewing_uses_application_mediated_opaque_access_and_permissioned_web_route
     * 18: Image_processing_package_decision_is_coherent_and_domain_application_are_package_neutral
     * 19: Historical_tasks_stage_note_is_interpreted_with_current_implementation_stage
     */

    [Fact]
    public async Task Central_artifact_identity_and_feature001_custody_state_remain_authoritative_after_upload()
    {
        var artifactId = await SeedOutOfStorageArtifactWithDeliveryAsync("QA");
        Feature001Snapshot before;
        await using (var beforeContext = postgres.CreateContext())
        {
            before = await LoadFeature001SnapshotAsync(beforeContext, artifactId);
        }

        await using (var actionContext = postgres.CreateContext())
        {
            var result = await NewCreateUseCase(actionContext, minio.CreateStorage(), "photographer-central")
                .CreatePhotographySetWithImages(CreateCommand(
                    artifactId,
                    PhotographyPurpose.DuringMaintenance,
                    "central-custody",
                    [UploadFile(0, "during-maintenance.jpg", PhotographyIntegrationTestImages.Jpeg(640, 480))]));

            Assert.True(result.Succeeded);
            Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        }

        await using var verification = postgres.CreateContext();
        var after = await LoadFeature001SnapshotAsync(verification, artifactId);
        var set = await verification.PhotographySets.AsNoTracking().SingleAsync(set => set.ArtifactId == artifactId);
        var image = await verification.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactId == artifactId);

        Assert.Equal(before.ArtifactId, set.ArtifactId);
        Assert.Equal(set.ArtifactId, image.ArtifactId);
        Assert.Equal(set.PhotographySetId, image.PhotographySetId);
        Assert.Equal(1, after.ArtifactRowsByArtifactId);
        Assert.Equal(1, after.ArtifactRowsByMuseumNumberDisplay);
        AssertFeature001StateUnchanged(before, after);
        Assert.Equal(ArtifactCurrentStatus.OutOfStorage, after.CurrentStatus);
        Assert.Equal(MovementRecipientType.LaboratoryDivision.ToString(), after.CurrentHolderType);
        Assert.DoesNotContain(after.Movements, movement => movement.RecipientType == MovementRecipientType.Photographer);
    }

    [Fact]
    public async Task Multi_image_upload_returns_file_outcomes_and_persisted_idempotency_survives_restart()
    {
        var artifactId = await SeedInStorageArtifactAsync("QI");

        UseCaseResult<PhotographyUploadOperationResultDto> first;
        await using (var firstContext = postgres.CreateContext())
        {
            first = await NewCreateUseCase(firstContext, minio.CreateStorage(), "photographer-idempotent")
                .CreatePhotographySetWithImages(IdempotencyCommand(artifactId));
        }

        Assert.True(first.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, first.Value!.Status);
        Assert.Equal(3, first.Value.FileResults.Count);
        Assert.Equal(
            [PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Rejected],
            first.Value.FileResults.OrderBy(file => file.ClientFileOrdinal).Select(file => file.Status).ToArray());
        Assert.All(first.Value.FileResults, file => Assert.DoesNotContain("artifact-images", file.StaffFacingMessage, StringComparison.OrdinalIgnoreCase));

        var firstSuccessfulIds = first.Value.FileResults
            .Where(file => file.Status == PhotographyUploadFileOutcomeStatus.Succeeded)
            .Select(file => file.ArtifactImageId)
            .ToArray();

        UseCaseResult<PhotographyUploadOperationResultDto> replay;
        await using (var replayContext = postgres.CreateContext())
        {
            replay = await NewCreateUseCase(replayContext, minio.CreateStorage(), "photographer-idempotent")
                .CreatePhotographySetWithImages(IdempotencyCommand(artifactId));
        }

        Assert.True(replay.Succeeded);
        Assert.Equal(first.Value.PhotographyUploadOperationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Equal(first.Value.PhotographySetId, replay.Value.PhotographySetId);
        Assert.Equal(firstSuccessfulIds, replay.Value.FileResults
            .Where(file => file.Status == PhotographyUploadFileOutcomeStatus.Succeeded)
            .Select(file => file.ArtifactImageId)
            .ToArray());

        UseCaseResult<PhotographyUploadOperationResultDto> conflictingReplay;
        await using (var conflictContext = postgres.CreateContext())
        {
            conflictingReplay = await NewCreateUseCase(conflictContext, minio.CreateStorage(), "photographer-idempotent")
                .CreatePhotographySetWithImages(CreateCommand(
                    artifactId,
                    PhotographyPurpose.GeneralDocumentation,
                    "idempotency-key",
                    [
                        UploadFile(0, "front.jpg", PhotographyIntegrationTestImages.Jpeg(600, 400)),
                        UploadFile(1, "side.png", PhotographyIntegrationTestImages.Png(320, 240))
                    ]));
        }

        Assert.False(conflictingReplay.Succeeded);
        Assert.True(conflictingReplay.ConcurrencyConflict);

        await using var verification = postgres.CreateContext();
        Assert.Equal(1, await verification.PhotographySets.CountAsync(set => set.ArtifactId == artifactId));
        Assert.Equal(2, await verification.ArtifactImages.CountAsync(image => image.ArtifactId == artifactId && image.Status == ArtifactImageStatus.Available));
        Assert.Equal(3, await verification.PhotographyUploadFileOutcomes.CountAsync(outcome => outcome.PhotographyUploadOperationId == first.Value.PhotographyUploadOperationId));
    }

    [Fact]
    public async Task Image_validation_accepts_jpeg_png_content_and_rejects_unsupported_or_spoofed_content()
    {
        var processor = NewImageProcessor();

        var jpg = await processor.ValidateAsync(Stream(PhotographyIntegrationTestImages.Jpeg(320, 240)), "front.jpg", PhotographyIntegrationTestImages.Jpeg(320, 240).LongLength);
        var pngBytes = PhotographyIntegrationTestImages.Png(320, 240);
        var png = await processor.ValidateAsync(Stream(pngBytes), "side.png", pngBytes.LongLength);
        var gifBytes = PhotographyIntegrationTestImages.Gif();
        var gif = await processor.ValidateAsync(Stream(gifBytes), "animated.gif", gifBytes.LongLength);
        var spoofed = await processor.ValidateAsync(Stream(gifBytes), "looks-like-jpeg.jpg", gifBytes.LongLength);

        Assert.True(jpg.IsValid);
        Assert.Equal(ArtifactImageFormat.Jpeg, jpg.Media!.Format);
        Assert.Equal("image/jpeg", jpg.Media.ContentType);
        Assert.True(png.IsValid);
        Assert.Equal(ArtifactImageFormat.Png, png.Media!.Format);
        Assert.Equal("image/png", png.Media.ContentType);
        Assert.False(gif.IsValid);
        Assert.False(spoofed.IsValid);

        var artifactId = await SeedInStorageArtifactAsync("QF");
        await using var actionContext = postgres.CreateContext();
        var upload = await NewCreateUseCase(actionContext, minio.CreateStorage(), "photographer-format")
            .CreatePhotographySetWithImages(CreateCommand(
                artifactId,
                PhotographyPurpose.GeneralDocumentation,
                "format-rejection",
                [
                    UploadFile(0, "accepted.jpg", PhotographyIntegrationTestImages.Jpeg(200, 200)),
                    UploadFile(1, "spoofed.jpg", gifBytes)
                ]));

        Assert.True(upload.Succeeded);
        Assert.Single(upload.Value!.FileResults, file => file.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
        Assert.Single(upload.Value.FileResults, file => file.Status == PhotographyUploadFileOutcomeStatus.Rejected && file.ArtifactImageId is null);
    }

    [Fact]
    public async Task Primary_image_state_has_one_authority_and_set_delete_concurrency_has_one_winner()
    {
        var seed = await UploadTwoImagesAsync("QP", "primary-single", "manager-primary");

        await using (var firstSetContext = postgres.CreateContext())
        {
            var firstSet = await NewSetPrimaryUseCase(firstSetContext, "manager-primary")
                .SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(seed.ArtifactId, seed.ImageAId, ExpectedConcurrencyToken: 0));
            Assert.True(firstSet.Succeeded);
        }

        int tokenAfterFirstSet;
        await using (var secondSetContext = postgres.CreateContext())
        {
            var current = await secondSetContext.ArtifactPhotographyStates.AsNoTracking().SingleAsync(state => state.ArtifactId == seed.ArtifactId);
            tokenAfterFirstSet = current.ConcurrencyToken;
            var secondSet = await NewSetPrimaryUseCase(secondSetContext, "manager-primary")
                .SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(seed.ArtifactId, seed.ImageBId, tokenAfterFirstSet));
            Assert.True(secondSet.Succeeded);
        }

        await using (var verifyPrimary = postgres.CreateContext())
        {
            var state = await verifyPrimary.ArtifactPhotographyStates.AsNoTracking().SingleAsync(state => state.ArtifactId == seed.ArtifactId);
            Assert.Equal(seed.ImageBId, state.PrimaryImageId);
            Assert.Equal(1, await verifyPrimary.ArtifactPhotographyStates.CountAsync(state => state.ArtifactId == seed.ArtifactId && state.PrimaryImageId != null));
            Assert.Equal(2, await verifyPrimary.ArtifactImages.CountAsync(image => image.ArtifactId == seed.ArtifactId && image.Status == ArtifactImageStatus.Available));
        }

        await using var deleteWins = postgres.CreateContext();
        await using var staleSet = postgres.CreateContext();
        var clearingState = await deleteWins.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == seed.ArtifactId);
        var staleState = await staleSet.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == seed.ArtifactId);

        clearingState.ClearPrimaryImage("deleter-primary");
        await deleteWins.SaveChangesAsync();

        staleState.SetPrimaryImage(seed.ImageAId, "manager-stale");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleSet.SaveChangesAsync());

        await using var reload = postgres.CreateContext();
        var authoritative = await reload.ArtifactPhotographyStates.AsNoTracking().SingleAsync(state => state.ArtifactId == seed.ArtifactId);
        Assert.Null(authoritative.PrimaryImageId);
        Assert.Equal(2, await reload.ArtifactImages.CountAsync(image => image.ArtifactId == seed.ArtifactId && image.Status == ArtifactImageStatus.Available));
    }

    [Fact]
    public async Task Request_completion_requires_upload_matching_fulfillment_and_explicit_per_request_completion()
    {
        var artifactId = await SeedInStorageArtifactAsync("QR");
        var set = await UploadOneImageAsync(artifactId, PhotographyPurpose.PreMaintenance, "request-fulfillment", "photographer-request");
        var firstRequest = await CreateRequestAsync(artifactId, PhotographyPurpose.PreMaintenance, "requester-a");
        var secondRequest = await CreateRequestAsync(artifactId, PhotographyPurpose.PreMaintenance, "requester-b");

        await using (var noUploadContext = postgres.CreateContext())
        {
            var denied = await NewCompleteRequestUseCase(noUploadContext, "manager-only", [PermissionNames.PhotographyManage])
                .CompletePhotographyRequest(new CompletePhotographyRequestCommand(firstRequest.PhotographyRequestId, set.SetId, firstRequest.ConcurrencyToken));
            Assert.False(denied.Succeeded);
            Assert.Contains(denied.ValidationIssues, issue => issue.Message.Contains(PermissionNames.PhotographyUpload, StringComparison.Ordinal));
        }

        var wrongPurposeSet = await UploadOneImageAsync(artifactId, PhotographyPurpose.PostMaintenance, "request-wrong-purpose", "photographer-request");
        await using (var mismatchContext = postgres.CreateContext())
        {
            var mismatch = await NewCompleteRequestUseCase(mismatchContext, "photographer-request", [PermissionNames.PhotographyUpload])
                .CompletePhotographyRequest(new CompletePhotographyRequestCommand(firstRequest.PhotographyRequestId, wrongPurposeSet.SetId, firstRequest.ConcurrencyToken));
            Assert.False(mismatch.Succeeded);
            Assert.Contains(mismatch.ValidationIssues, issue => issue.Code == "PhotographySet.PurposeConflict");
        }

        await using (var firstCompletionContext = postgres.CreateContext())
        {
            var completed = await NewCompleteRequestUseCase(firstCompletionContext, "photographer-request", [PermissionNames.PhotographyUpload])
                .CompletePhotographyRequest(new CompletePhotographyRequestCommand(firstRequest.PhotographyRequestId, set.SetId, firstRequest.ConcurrencyToken));
            Assert.True(completed.Succeeded);
            Assert.Equal(PhotographyRequestStatus.Completed, completed.Value!.Status);
        }

        await using (var betweenContext = postgres.CreateContext())
        {
            var stillPending = await betweenContext.PhotographyRequests.AsNoTracking().SingleAsync(request => request.PhotographyRequestId == secondRequest.PhotographyRequestId);
            Assert.Equal(PhotographyRequestStatus.Pending, stillPending.Status);
            Assert.Null(stillPending.FulfillingPhotographySetId);
        }

        await using (var secondCompletionContext = postgres.CreateContext())
        {
            var freshSecond = await secondCompletionContext.PhotographyRequests.AsNoTracking().SingleAsync(request => request.PhotographyRequestId == secondRequest.PhotographyRequestId);
            var completed = await NewCompleteRequestUseCase(secondCompletionContext, "photographer-request", [PermissionNames.PhotographyUpload])
                .CompletePhotographyRequest(new CompletePhotographyRequestCommand(freshSecond.PhotographyRequestId, set.SetId, freshSecond.ConcurrencyToken));
            Assert.True(completed.Succeeded);
        }

        await using var verification = postgres.CreateContext();
        var completedRequests = await verification.PhotographyRequests.AsNoTracking()
            .Where(request => request.FulfillingPhotographySetId == set.SetId)
            .OrderBy(request => request.RequestedByUserId)
            .ToListAsync();
        Assert.Equal(2, completedRequests.Count);
        Assert.All(completedRequests, request => Assert.Equal(PhotographyRequestStatus.Completed, request.Status));
    }

    [Fact]
    public async Task Append_rejects_wrong_artifact_or_purpose_without_mutating_the_set()
    {
        var artifactId = await SeedInStorageArtifactAsync("QE");
        var otherArtifactId = await SeedInStorageArtifactAsync("QO");
        var existing = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "append-base", "photographer-append");

        await using (var wrongArtifactContext = postgres.CreateContext())
        {
            var result = await NewAppendUseCase(wrongArtifactContext, minio.CreateStorage(), "photographer-append")
                .AppendImagesToPhotographySet(new AppendImagesToPhotographySetCommand(
                    existing.SetId,
                    $"append-wrong-artifact-{Guid.NewGuid():N}",
                    [UploadFile(0, "wrong-artifact.jpg", PhotographyIntegrationTestImages.Jpeg(320, 240))],
                    ArtifactIdConfirmation: otherArtifactId,
                    PurposeConfirmation: PhotographyPurpose.GeneralDocumentation));

            Assert.False(result.Succeeded);
            Assert.Contains(result.ValidationIssues, issue => issue.Code == "PhotographySet.ArtifactConflict");
        }

        await using (var wrongPurposeContext = postgres.CreateContext())
        {
            var result = await NewAppendUseCase(wrongPurposeContext, minio.CreateStorage(), "photographer-append")
                .AppendImagesToPhotographySet(new AppendImagesToPhotographySetCommand(
                    existing.SetId,
                    $"append-wrong-purpose-{Guid.NewGuid():N}",
                    [UploadFile(0, "wrong-purpose.png", PhotographyIntegrationTestImages.Png(320, 240))],
                    ArtifactIdConfirmation: artifactId,
                    PurposeConfirmation: PhotographyPurpose.PostMaintenance));

            Assert.False(result.Succeeded);
            Assert.Contains(result.ValidationIssues, issue => issue.Code == "PhotographySet.PurposeConflict");
        }

        await using var verification = postgres.CreateContext();
        Assert.Equal(1, await verification.ArtifactImages.CountAsync(image => image.PhotographySetId == existing.SetId && image.Status == ArtifactImageStatus.Available));
        Assert.Equal(0, await verification.PhotographyUploadOperations.CountAsync(operation => operation.OperationKind == PhotographyUploadOperationKind.AppendToSetUpload && operation.PhotographySetId == existing.SetId));
    }

    [Fact]
    public async Task Grace_and_privileged_deletion_use_actual_permission_reason_and_time_boundaries()
    {
        var artifactId = await SeedInStorageArtifactAsync("QD");
        var allowed = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "grace-allowed", "photographer-delete");
        var denied = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "grace-denied", "photographer-delete");
        var privileged = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "privileged-delete", "photographer-delete");

        await using (var allowedContext = postgres.CreateContext())
        {
            var image = await allowedContext.ArtifactImages.AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == allowed.ImageId);
            var result = await NewGraceDeleteUseCase(
                    allowedContext,
                    minio.CreateStorage(),
                    "photographer-delete",
                    [PermissionNames.PhotographyUpload],
                    image.UploadedAt.AddMinutes(60))
                .DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(image.ArtifactImageId, image.ConcurrencyToken));

            Assert.True(result.Succeeded);
            Assert.Equal(ArtifactImageDeletionMode.UploaderGracePeriod, result.Value!.DeletionMode);
        }

        await using (var deniedContext = postgres.CreateContext())
        {
            var image = await deniedContext.ArtifactImages.AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == denied.ImageId);
            var result = await NewGraceDeleteUseCase(
                    deniedContext,
                    minio.CreateStorage(),
                    "photographer-delete",
                    [PermissionNames.PhotographyUpload],
                    image.UploadedAt.AddMinutes(60).AddTicks(1))
                .DeleteArtifactImageByUploaderGrace(new DeleteArtifactImageByUploaderGraceCommand(image.ArtifactImageId, image.ConcurrencyToken));

            Assert.False(result.Succeeded);
            Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.GracePeriodExpired");
        }

        await using (var noReasonContext = postgres.CreateContext())
        {
            var image = await noReasonContext.ArtifactImages.AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == privileged.ImageId);
            var result = await NewPrivilegedDeleteUseCase(
                    noReasonContext,
                    minio.CreateStorage(),
                    "supervisor-delete",
                    [PermissionNames.PhotographyDelete],
                    image.UploadedAt.AddHours(2))
                .DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(image.ArtifactImageId, "   ", image.ConcurrencyToken));

            Assert.False(result.Succeeded);
            Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionReasonRequired");
        }

        await using (var privilegedContext = postgres.CreateContext())
        {
            var image = await privilegedContext.ArtifactImages.AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == privileged.ImageId);
            var result = await NewPrivilegedDeleteUseCase(
                    privilegedContext,
                    minio.CreateStorage(),
                    "supervisor-delete",
                    [PermissionNames.PhotographyDelete],
                    image.UploadedAt.AddHours(2))
                .DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(image.ArtifactImageId, "Curatorial duplicate", image.ConcurrencyToken));

            Assert.True(result.Succeeded);
            Assert.Equal(ArtifactImageDeletionMode.Privileged, result.Value!.DeletionMode);
        }

        await using var verification = postgres.CreateContext();
        var deniedReload = await verification.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == denied.ImageId);
        var privilegedReload = await verification.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == privileged.ImageId);
        Assert.Equal(ArtifactImageStatus.Available, deniedReload.Status);
        Assert.Equal(ArtifactImageStatus.Deleted, privilegedReload.Status);
        Assert.Equal("Curatorial duplicate", privilegedReload.DeletionReason);
    }

    [Fact]
    public async Task Permanent_deletion_removes_original_thumbnail_and_preview_objects()
    {
        var artifactId = await SeedInStorageArtifactAsync("QB");
        var uploaded = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "binary-cleanup", "photographer-binary");

        ImageStorageObjectKey original;
        ImageStorageObjectKey[] derivatives;
        int concurrencyToken;
        await using (var loadContext = postgres.CreateContext())
        {
            var image = await loadContext.ArtifactImages.Include(image => image.Derivatives).AsNoTracking().SingleAsync(image => image.ArtifactImageId == uploaded.ImageId);
            original = image.OriginalObjectKey;
            derivatives = image.Derivatives.OrderBy(derivative => derivative.Kind).Select(derivative => derivative.ObjectKey).ToArray();
            concurrencyToken = image.ConcurrencyToken;
            Assert.Equal(2, derivatives.Length);
            Assert.All([original, .. derivatives], key => Assert.True(minio.CreateStorage().StatAsync(key).AsTask().GetAwaiter().GetResult().Exists));
        }

        await using (var deleteContext = postgres.CreateContext())
        {
            var result = await NewPrivilegedDeleteUseCase(
                    deleteContext,
                    minio.CreateStorage(),
                    "supervisor-binary",
                    [PermissionNames.PhotographyDelete],
                    DateTimeOffset.UtcNow)
                .DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(uploaded.ImageId, "Remove duplicate binary set", concurrencyToken));

            Assert.True(result.Succeeded);
        }

        var storage = minio.CreateStorage();
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(original)).Kind);
        foreach (var derivative in derivatives)
        {
            Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(derivative)).Kind);
        }

        await using var verification = postgres.CreateContext();
        var deleted = await verification.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == uploaded.ImageId);
        var audit = await verification.AuditEntries.AsNoTracking().SingleAsync(audit => audit.EntityId == uploaded.ImageId.ToString() && audit.ActionName == PhotographyAuditActions.ImageDeletePrivileged);
        Assert.Equal(ArtifactImageStatus.Deleted, deleted.Status);
        Assert.Contains(uploaded.ImageId.ToString(), audit.ChangeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Storage_cleanup_failure_leaves_recoverable_auditable_state_without_staff_storage_internals()
    {
        var artifactId = await SeedInStorageArtifactAsync("QC");
        var uploaded = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "delete-recovery", "photographer-recovery");
        var failingStorage = new FailingDeleteStorage(minio.CreateStorage());

        UseCaseResult<ArtifactImageDeletionDto> result;
        await using (var deleteContext = postgres.CreateContext())
        {
            var image = await deleteContext.ArtifactImages.AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == uploaded.ImageId);
            result = await NewPrivilegedDeleteUseCase(
                    deleteContext,
                    failingStorage,
                    "supervisor-recovery",
                    [PermissionNames.PhotographyDelete],
                    RecoveryClock)
                .DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(uploaded.ImageId, "Storage failure proof", image.ConcurrencyToken));
        }

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionRecoveryRequired");
        AssertSafeStaffText(string.Join(" ", result.ValidationIssues.Select(issue => issue.Message)), failingStorage.FailedKeys.Select(key => key.Value).ToArray());

        await using var verification = postgres.CreateContext();
        var imageState = await verification.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == uploaded.ImageId);
        var recovery = await verification.StorageOperationRecoveries.AsNoTracking().SingleAsync(recovery => recovery.ArtifactImageId == uploaded.ImageId);
        Assert.Equal(ArtifactImageStatus.DeletePending, imageState.Status);
        Assert.Equal(StorageOperationRecoveryType.DeleteCleanup, recovery.OperationType);
        Assert.Equal(StorageOperationRecoveryStatus.Pending, recovery.Status);
        Assert.Equal(uploaded.ImageId, recovery.ArtifactImageId);
        Assert.NotEmpty(recovery.ObjectKeys);
        AssertSafeStaffText(recovery.FailureSummary, minio.Options.Endpoint, minio.Options.BucketName, minio.Options.AccessKey, minio.Options.SecretKey);
    }

    [Fact]
    public async Task Deletion_finalization_failure_keeps_delete_pending_and_retry_finalizes_without_restoring_binary()
    {
        var artifactId = await SeedInStorageArtifactAsync("QZ");
        var uploaded = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "finalization-retry", "photographer-finalize");

        ImageStorageObjectKey original;
        ImageStorageObjectKey[] derivatives;
        int pendingToken;
        await using (var deleteContext = postgres.CreateContext())
        {
            var image = await deleteContext.ArtifactImages.Include(candidate => candidate.Derivatives).AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == uploaded.ImageId);
            original = image.OriginalObjectKey;
            derivatives = image.Derivatives.Select(derivative => derivative.ObjectKey).ToArray();
            var deletionService = new ArtifactImageDeletionService(
                deleteContext,
                new ThrowingAuditWriter(),
                minio.CreateStorage(),
                new ArtifactImageDeletionFinalizationService(deleteContext, new ThrowingAuditWriter(), new FixedTimeProvider(RecoveryClock)));
            var useCase = new DeleteArtifactImagePrivilegedUseCase(
                deleteContext,
                new TestAuditActorContext("supervisor-finalize"),
                new StaticPermissionChecker([PermissionNames.PhotographyDelete]),
                new FixedTimeProvider(RecoveryClock),
                deletionService);

            var failed = await useCase.DeleteArtifactImagePrivileged(new DeleteArtifactImagePrivilegedCommand(uploaded.ImageId, "Audit retry proof", image.ConcurrencyToken));
            Assert.False(failed.Succeeded);
            Assert.Contains(failed.ValidationIssues, issue => issue.Code == "ArtifactImage.DeletionFinalizationPending");

            var pending = await deleteContext.ArtifactImages.AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == uploaded.ImageId);
            pendingToken = pending.ConcurrencyToken;
        }

        var storage = minio.CreateStorage();
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(original)).Kind);
        foreach (var derivative in derivatives)
        {
            Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(derivative)).Kind);
        }

        await using (var retryContext = postgres.CreateContext())
        {
            var pending = await retryContext.ArtifactImages.AsNoTracking().SingleAsync(candidate => candidate.ArtifactImageId == uploaded.ImageId);
            Assert.Equal(ArtifactImageStatus.DeletePending, pending.Status);
            Assert.Equal(pendingToken, pending.ConcurrencyToken);

            var retry = await new ArtifactImageDeletionFinalizationService(
                    retryContext,
                    new AuditWriter(retryContext, new TestAuditActorContext("recovery-worker-finalize")),
                    new FixedTimeProvider(RecoveryClock.AddMinutes(1)))
                .FinalizeAsync(new ArtifactImageDeletionFinalizationRequest(uploaded.ImageId, ArtifactImageDeletionMode.Privileged, pending.ConcurrencyToken));

            Assert.Equal(ArtifactImageDeletionFinalizationOutcome.Completed, retry.Outcome);
        }

        await using var verification = postgres.CreateContext();
        var deleted = await verification.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == uploaded.ImageId);
        Assert.Equal(ArtifactImageStatus.Deleted, deleted.Status);
        Assert.Equal(ArtifactImageStorageResultKind.NotFound, (await storage.StatAsync(original)).Kind);
    }

    [Fact]
    public async Task Staff_viewing_uses_application_mediated_opaque_access_and_permissioned_web_route()
    {
        var artifactId = await SeedInStorageArtifactAsync("QV");
        var uploaded = await UploadOneImageAsync(artifactId, PhotographyPurpose.GeneralDocumentation, "opaque-view", "photographer-view");

        await using (var deniedContext = postgres.CreateContext())
        {
            var denied = await new ViewArtifactImagesUseCase(
                    deniedContext,
                    new StaticPermissionChecker([]),
                    minio.CreateStorage(),
                    new PhotographyGalleryMapper())
                .ViewArtifactImages(new ViewArtifactImagesQuery(artifactId));

            Assert.False(denied.Succeeded);
            Assert.Contains(denied.ValidationIssues, issue => issue.Message.Contains(PermissionNames.PhotographyView, StringComparison.Ordinal));
        }

        await using (var viewContext = postgres.CreateContext())
        {
            var useCase = new ViewArtifactImagesUseCase(
                viewContext,
                new StaticPermissionChecker([PermissionNames.PhotographyView]),
                minio.CreateStorage(),
                new PhotographyGalleryMapper());
            var gallery = await useCase.ViewArtifactImages(new ViewArtifactImagesQuery(artifactId));

            Assert.True(gallery.Succeeded);
            var image = Assert.Single(gallery.Value!.Images);
            Assert.Equal(uploaded.ImageId, image.ArtifactImageId);
            Assert.Equal(PhotographyImageRenditionAvailability.Available, image.Thumbnail.Availability);
            Assert.Equal(PhotographyImageRenditionAvailability.Available, image.Preview.Availability);
            Assert.NotNull(image.Preview.Access);
            AssertSafeStaffText(image.Preview.Access!.ToString(), minio.Options.Endpoint, minio.Options.BucketName, "artifact-images", minio.Options.AccessKey, minio.Options.SecretKey);

            await using var stream = (await useCase.ReadArtifactImageRendition(new ReadArtifactImageRenditionQuery(uploaded.ImageId, PhotographyImageRendition.Preview))).Value!;
            Assert.Equal(PhotographyImageStreamStatus.Available, stream.Status);
            Assert.NotNull(stream.Content);
            Assert.Equal("image/jpeg", stream.ContentType);
        }

        var root = FindRepositoryRoot();
        var endpointSource = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Photography", "ImageStreamEndpoint.cs"));
        Assert.Contains("/photography/images/{artifactImageId:guid}/{rendition}", endpointSource);
        Assert.Contains("RequireAuthorization(PermissionNames.PhotographyView)", endpointSource);
        Assert.DoesNotContain("Presigned", endpointSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BucketName", endpointSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Image_processing_package_decision_is_coherent_and_domain_application_are_package_neutral()
    {
        var root = FindRepositoryRoot();
        var decisions = File.ReadAllText(Path.Combine(root.FullName, "specs", "003-artifact-photography-image-stewardship", "implementation-decisions.md"));
        var infrastructure = XDocument.Load(Path.Combine(root.FullName, "src", "MuseumSystem.Infrastructure", "MuseumSystem.Infrastructure.csproj"));
        var application = XDocument.Load(Path.Combine(root.FullName, "src", "MuseumSystem.Application", "MuseumSystem.Application.csproj"));
        var domain = XDocument.Load(Path.Combine(root.FullName, "src", "MuseumSystem.Domain", "MuseumSystem.Domain.csproj"));

        AssertPackage(infrastructure, "SkiaSharp", "4.151.1");
        AssertPackage(infrastructure, "SkiaSharp.NativeAssets.Linux", "4.151.1");
        Assert.DoesNotContain(PackageReferences(infrastructure), package => package.Include.Contains("ImageSharp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(PackageReferences(infrastructure), package => package.Include == "System.Drawing.Common");
        Assert.Contains("MIT licensed", decisions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT licensing is compatible", decisions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Selected over `SixLabors.ImageSharp`", decisions, StringComparison.Ordinal);
        Assert.Contains("Selected over `System.Drawing.Common`", decisions, StringComparison.Ordinal);
        Assert.DoesNotContain(PackageReferences(application), package => package.Include.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(PackageReferences(domain), package => package.Include.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Historical_tasks_stage_note_is_interpreted_with_current_implementation_stage()
    {
        var root = FindRepositoryRoot();
        var quickstartPath = Path.Combine(root.FullName, "specs", "003-artifact-photography-image-stewardship", "quickstart.md");
        var tasksPath = Path.Combine(root.FullName, "specs", "003-artifact-photography-image-stewardship", "tasks.md");
        var quickstart = File.ReadAllText(quickstartPath);
        var tasks = File.ReadAllText(tasksPath);

        Assert.Contains("No `tasks.md` should exist until the tasks stage.", quickstart, StringComparison.Ordinal);
        Assert.True(File.Exists(tasksPath));
        Assert.Contains("T125 [P] Add quickstart validation tests", tasks, StringComparison.Ordinal);
        Assert.Contains("- [x] T122", tasks, StringComparison.Ordinal);
        Assert.Contains("- [x] T123", tasks, StringComparison.Ordinal);
        Assert.Contains("- [x] T124", tasks, StringComparison.Ordinal);
        Assert.Contains("- [ ] T126", tasks, StringComparison.Ordinal);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (createdArtifactIds.Count == 0)
        {
            return;
        }

        await using var db = postgres.CreateContext();
        var operationIds = await db.PhotographyUploadOperations
            .Where(operation => createdArtifactIds.Contains(operation.ArtifactId))
            .Select(operation => operation.PhotographyUploadOperationId)
            .ToListAsync();
        var outcomeIds = operationIds.Count == 0
            ? []
            : await db.PhotographyUploadFileOutcomes
                .Where(outcome => operationIds.Contains(outcome.PhotographyUploadOperationId))
                .Select(outcome => outcome.PhotographyUploadFileOutcomeId)
                .ToListAsync();
        var recoveryIds = await db.StorageOperationRecoveries
            .Where(recovery => createdArtifactIds.Contains(recovery.ArtifactId))
            .Select(recovery => recovery.StorageOperationRecoveryId)
            .ToListAsync();
        var requestIds = await db.PhotographyRequests
            .Where(request => createdArtifactIds.Contains(request.ArtifactId))
            .Select(request => request.PhotographyRequestId)
            .ToListAsync();
        var imageIds = await db.ArtifactImages
            .Where(image => createdArtifactIds.Contains(image.ArtifactId))
            .Select(image => image.ArtifactImageId)
            .ToListAsync();
        var setIds = await db.PhotographySets
            .Where(set => createdArtifactIds.Contains(set.ArtifactId))
            .Select(set => set.PhotographySetId)
            .ToListAsync();
        var stateIds = await db.ArtifactPhotographyStates
            .Where(state => createdArtifactIds.Contains(state.ArtifactId))
            .Select(state => state.ArtifactId)
            .ToListAsync();

        var objectKeys = await db.ArtifactImages
            .Include(image => image.Derivatives)
            .Where(image => createdArtifactIds.Contains(image.ArtifactId))
            .SelectMany(image => new[] { image.OriginalObjectKey }.Concat(image.Derivatives.Select(derivative => derivative.ObjectKey)))
            .ToListAsync();
        objectKeys.AddRange((await db.StorageOperationRecoveries
            .Where(recovery => recoveryIds.Contains(recovery.StorageOperationRecoveryId))
            .ToListAsync())
            .SelectMany(recovery => recovery.ObjectKeys));

        var storage = minio.CreateStorage();
        foreach (var key in objectKeys.DistinctBy(key => key.Value))
        {
            await storage.DeleteObjectAsync(key);
        }

        var auditEntityIds = createdArtifactIds
            .Concat(operationIds)
            .Concat(outcomeIds)
            .Concat(recoveryIds)
            .Concat(requestIds)
            .Concat(imageIds)
            .Concat(setIds)
            .Concat(stateIds)
            .Select(id => id.ToString())
            .ToArray();
        if (auditEntityIds.Length > 0)
        {
            db.AuditEntries.RemoveRange(await db.AuditEntries.Where(audit => auditEntityIds.Contains(audit.EntityId)).ToListAsync());
        }

        db.PhotographyRequests.RemoveRange(await db.PhotographyRequests.Where(request => createdArtifactIds.Contains(request.ArtifactId)).ToListAsync());
        db.ArtifactPhotographyStates.RemoveRange(await db.ArtifactPhotographyStates.Where(state => createdArtifactIds.Contains(state.ArtifactId)).ToListAsync());
        db.StorageOperationRecoveries.RemoveRange(await db.StorageOperationRecoveries.Where(recovery => recoveryIds.Contains(recovery.StorageOperationRecoveryId)).ToListAsync());
        if (operationIds.Count > 0)
        {
            db.PhotographyUploadFileOutcomes.RemoveRange(await db.PhotographyUploadFileOutcomes.Where(outcome => operationIds.Contains(outcome.PhotographyUploadOperationId)).ToListAsync());
            db.PhotographyUploadOperations.RemoveRange(await db.PhotographyUploadOperations.Where(operation => operationIds.Contains(operation.PhotographyUploadOperationId)).ToListAsync());
        }

        db.ArtifactImageDerivatives.RemoveRange(await db.ArtifactImageDerivatives.Where(derivative => imageIds.Contains(derivative.ArtifactImageId)).ToListAsync());
        db.ArtifactImages.RemoveRange(await db.ArtifactImages.Where(image => imageIds.Contains(image.ArtifactImageId)).ToListAsync());
        db.PhotographySets.RemoveRange(await db.PhotographySets.Where(set => createdArtifactIds.Contains(set.ArtifactId)).ToListAsync());
        db.MovementRecords.RemoveRange(await db.MovementRecords.Where(record => createdArtifactIds.Contains(record.ArtifactId)).ToListAsync());

        var artifacts = await db.Artifacts.Where(artifact => createdArtifactIds.Contains(artifact.ArtifactId)).ToListAsync();
        var categoryIds = artifacts.Select(artifact => artifact.CategoryId).Distinct().ToArray();
        var locationIds = artifacts.SelectMany(artifact => new[] { artifact.CurrentLocationId, artifact.LastKnownStorageLocationId }).OfType<Guid>().Distinct().ToArray();
        db.Artifacts.RemoveRange(artifacts);
        await db.SaveChangesAsync();

        db.ArtifactCategories.RemoveRange(await db.ArtifactCategories.Where(category => categoryIds.Contains(category.CategoryId)).ToListAsync());
        db.Locations.RemoveRange(await db.Locations.Where(location => locationIds.Contains(location.LocationId)).ToListAsync());
        await db.SaveChangesAsync();
    }

    private async Task<Guid> SeedInStorageArtifactAsync(string prefix)
    {
        await using var context = postgres.CreateContext();
        var category = ArtifactCategory.Create($"{prefix}{Guid.NewGuid():N}"[..8], $"{prefix} quickstart category");
        var storage = Location.Create($"{prefix} quickstart storage {Guid.NewGuid():N}", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, $"{prefix} quickstart artifact", storage);

        context.ArtifactCategories.Add(category);
        context.Locations.Add(storage);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();
        createdArtifactIds.Add(artifact.ArtifactId);
        return artifact.ArtifactId;
    }

    private async Task<Guid> SeedOutOfStorageArtifactWithDeliveryAsync(string prefix)
    {
        await using var context = postgres.CreateContext();
        var category = ArtifactCategory.Create($"{prefix}{Guid.NewGuid():N}"[..8], $"{prefix} quickstart category");
        var storage = Location.Create($"{prefix} quickstart storage {Guid.NewGuid():N}", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, $"{prefix} quickstart delivered artifact", storage);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Laboratory conservation bench");
        var movement = MovementRecord.CreateDelivery(
            Guid.NewGuid(),
            artifact,
            MovementRecipientType.LaboratoryDivision,
            "Laboratory conservation bench",
            "Quickstart custody proof",
            "Photography must not create custody",
            "registrar-quickstart");

        context.ArtifactCategories.Add(category);
        context.Locations.Add(storage);
        context.Artifacts.Add(artifact);
        context.MovementRecords.Add(movement);
        await context.SaveChangesAsync();
        createdArtifactIds.Add(artifact.ArtifactId);
        return artifact.ArtifactId;
    }

    private async Task<UploadSeed> UploadOneImageAsync(Guid artifactId, PhotographyPurpose purpose, string key, string actorUserId)
    {
        await using var context = postgres.CreateContext();
        var result = await NewCreateUseCase(context, minio.CreateStorage(), actorUserId)
            .CreatePhotographySetWithImages(CreateCommand(
                artifactId,
                purpose,
                $"{key}-{Guid.NewGuid():N}",
                [UploadFile(0, $"{key}.jpg", PhotographyIntegrationTestImages.Jpeg(360, 240))]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        var imageId = result.Value.FileResults.Single(file => file.Status == PhotographyUploadFileOutcomeStatus.Succeeded).ArtifactImageId!.Value;
        return new UploadSeed(artifactId, result.Value.PhotographySetId!.Value, imageId);
    }

    private async Task<TwoImageUploadSeed> UploadTwoImagesAsync(string prefix, string key, string actorUserId)
    {
        var artifactId = await SeedInStorageArtifactAsync(prefix);
        await using var context = postgres.CreateContext();
        var result = await NewCreateUseCase(context, minio.CreateStorage(), actorUserId)
            .CreatePhotographySetWithImages(CreateCommand(
                artifactId,
                PhotographyPurpose.GeneralDocumentation,
                $"{key}-{Guid.NewGuid():N}",
                [
                    UploadFile(0, $"{key}-a.jpg", PhotographyIntegrationTestImages.Jpeg(360, 240)),
                    UploadFile(1, $"{key}-b.png", PhotographyIntegrationTestImages.Png(300, 220))
                ]));

        Assert.True(result.Succeeded);
        var imageIds = result.Value!.FileResults.OrderBy(file => file.ClientFileOrdinal).Select(file => file.ArtifactImageId!.Value).ToArray();
        return new TwoImageUploadSeed(artifactId, result.Value.PhotographySetId!.Value, imageIds[0], imageIds[1]);
    }

    private async Task<PhotographyRequestDto> CreateRequestAsync(Guid artifactId, PhotographyPurpose purpose, string requesterUserId)
    {
        await using var context = postgres.CreateContext();
        var result = await new CreatePhotographyRequestUseCase(
                context,
                new TestAuditActorContext(requesterUserId),
                new StaticPermissionChecker([PermissionNames.PhotographyRequest]),
                new AuditWriter(context, new TestAuditActorContext(requesterUserId)),
                new FixedTimeProvider(RequestClock))
            .CreatePhotographyRequest(new CreatePhotographyRequestCommand(artifactId, purpose));

        Assert.True(result.Succeeded);
        return result.Value!;
    }

    private static CreatePhotographySetWithImagesUseCase NewCreateUseCase(MuseumDbContext db, IArtifactImageStorage storage, string actorUserId)
    {
        var persistence = new PhotographyUploadPersistenceService(db);
        var audit = new PhotographyUploadAuditService(new AuditWriter(db, new TestAuditActorContext(actorUserId)));
        return new CreatePhotographySetWithImagesUseCase(
            persistence,
            NewImageProcessor(),
            new PhotographyUploadFingerprintService(),
            new PhotographyUploadConsistencyService(
                persistence,
                storage,
                new PhotographyObjectKeyFactory(),
                new ArtifactImageStorageHealthService(),
                audit),
            new PhotographyResponseMapper(),
            new TestAuditActorContext(actorUserId));
    }

    private static AppendImagesToPhotographySetUseCase NewAppendUseCase(MuseumDbContext db, IArtifactImageStorage storage, string actorUserId)
    {
        var persistence = new PhotographyUploadPersistenceService(db);
        var audit = new PhotographyUploadAuditService(new AuditWriter(db, new TestAuditActorContext(actorUserId)));
        return new AppendImagesToPhotographySetUseCase(
            persistence,
            NewImageProcessor(),
            new PhotographyUploadFingerprintService(),
            new PhotographyUploadConsistencyService(
                persistence,
                storage,
                new PhotographyObjectKeyFactory(),
                new ArtifactImageStorageHealthService(),
                audit),
            new PhotographyResponseMapper(),
            new TestAuditActorContext(actorUserId));
    }

    private static CompletePhotographyRequestUseCase NewCompleteRequestUseCase(MuseumDbContext db, string actorUserId, IReadOnlyCollection<string> permissions) =>
        new(
            db,
            new TestAuditActorContext(actorUserId),
            new StaticPermissionChecker(permissions),
            new AuditWriter(db, new TestAuditActorContext(actorUserId)),
            new FixedTimeProvider(RequestClock.AddHours(1)));

    private static SetPrimaryArtifactImageUseCase NewSetPrimaryUseCase(MuseumDbContext db, string actorUserId) =>
        new(
            db,
            new TestAuditActorContext(actorUserId),
            new StaticPermissionChecker([PermissionNames.PhotographyManage]),
            new AuditWriter(db, new TestAuditActorContext(actorUserId)),
            new ArtifactPhotographyStateService(db),
            new FixedTimeProvider(PrimaryClock));

    private static DeleteArtifactImageByUploaderGraceUseCase NewGraceDeleteUseCase(
        MuseumDbContext db,
        IArtifactImageStorage storage,
        string actorUserId,
        IReadOnlyCollection<string> permissions,
        DateTimeOffset now)
    {
        var auditWriter = new AuditWriter(db, new TestAuditActorContext(actorUserId));
        var finalization = new ArtifactImageDeletionFinalizationService(db, auditWriter, new FixedTimeProvider(now));
        var deletion = new ArtifactImageDeletionService(db, auditWriter, storage, finalization);
        return new DeleteArtifactImageByUploaderGraceUseCase(
            db,
            new TestAuditActorContext(actorUserId),
            new StaticPermissionChecker(permissions),
            new FixedTimeProvider(now),
            deletion);
    }

    private static DeleteArtifactImagePrivilegedUseCase NewPrivilegedDeleteUseCase(
        MuseumDbContext db,
        IArtifactImageStorage storage,
        string actorUserId,
        IReadOnlyCollection<string> permissions,
        DateTimeOffset now)
    {
        var auditWriter = new AuditWriter(db, new TestAuditActorContext(actorUserId));
        var finalization = new ArtifactImageDeletionFinalizationService(db, auditWriter, new FixedTimeProvider(now));
        var deletion = new ArtifactImageDeletionService(db, auditWriter, storage, finalization);
        return new DeleteArtifactImagePrivilegedUseCase(
            db,
            new TestAuditActorContext(actorUserId),
            new StaticPermissionChecker(permissions),
            new FixedTimeProvider(now),
            deletion);
    }

    private static IArtifactImageProcessor NewImageProcessor() =>
        new ArtifactImageProcessor(Options.Create(new ArtifactImageProcessingOptions
        {
            MaximumOriginalBytes = 20 * 1024 * 1024,
            Thumbnail = new DerivativeOptions(320, 320, 82),
            Preview = new DerivativeOptions(1600, 1600, 86)
        }));

    private static CreatePhotographySetWithImagesCommand IdempotencyCommand(Guid artifactId) =>
        CreateCommand(
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            "idempotency-key",
            [
                UploadFile(0, "front.jpg", PhotographyIntegrationTestImages.Jpeg(640, 480)),
                UploadFile(1, "side.png", PhotographyIntegrationTestImages.Png(320, 240)),
                UploadFile(2, "notes.jpg", "not an image"u8.ToArray())
            ]);

    private static CreatePhotographySetWithImagesCommand CreateCommand(
        Guid artifactId,
        PhotographyPurpose purpose,
        string idempotencyKey,
        IReadOnlyList<PhotographyUploadFileInput> files) =>
        new(artifactId, purpose, new DateOnly(2026, 9, 1), "photographer", idempotencyKey, files);

    private static PhotographyUploadFileInput UploadFile(int ordinal, string filename, byte[] bytes) =>
        new(ordinal, filename, Stream(bytes), bytes.LongLength);

    private static MemoryStream Stream(byte[] bytes) => new(bytes, writable: false);

    private static async Task<Feature001Snapshot> LoadFeature001SnapshotAsync(MuseumDbContext context, Guid artifactId)
    {
        var artifact = await context.Artifacts.AsNoTracking().SingleAsync(row => row.ArtifactId == artifactId);
        var movements = await context.MovementRecords
            .AsNoTracking()
            .Where(record => record.ArtifactId == artifactId)
            .OrderBy(record => record.MovementId)
            .Select(record => new MovementSnapshot(
                record.MovementId,
                record.MovementType,
                record.MovementGroupId,
                record.ArtifactId,
                record.RecipientType,
                record.RecipientName,
                record.Purpose,
                record.ReturnLocationId,
                record.Note,
                record.OccurredAt,
                record.RecordedBy))
            .ToListAsync();

        return new Feature001Snapshot(
            artifact.ArtifactId,
            artifact.MuseumNumberDisplay,
            artifact.CategoryId,
            artifact.CurrentStatus,
            artifact.CurrentLocationId,
            artifact.CurrentHolderType,
            artifact.CurrentHolderName,
            artifact.LastKnownStorageLocationId,
            movements,
            await context.Artifacts.CountAsync(row => row.ArtifactId == artifact.ArtifactId),
            await context.Artifacts.CountAsync(row => row.MuseumNumberDisplay == artifact.MuseumNumberDisplay));
    }

    private static void AssertFeature001StateUnchanged(Feature001Snapshot before, Feature001Snapshot after)
    {
        Assert.Equal(before.ArtifactId, after.ArtifactId);
        Assert.Equal(before.MuseumNumberDisplay, after.MuseumNumberDisplay);
        Assert.Equal(before.CategoryId, after.CategoryId);
        Assert.Equal(before.CurrentStatus, after.CurrentStatus);
        Assert.Equal(before.CurrentLocationId, after.CurrentLocationId);
        Assert.Equal(before.CurrentHolderType, after.CurrentHolderType);
        Assert.Equal(before.CurrentHolderName, after.CurrentHolderName);
        Assert.Equal(before.LastKnownStorageLocationId, after.LastKnownStorageLocationId);
        Assert.Equal(before.Movements, after.Movements);
        Assert.Equal(before.ArtifactRowsByArtifactId, after.ArtifactRowsByArtifactId);
        Assert.Equal(before.ArtifactRowsByMuseumNumberDisplay, after.ArtifactRowsByMuseumNumberDisplay);
    }

    private static void AssertPackage(XDocument project, string include, string version)
    {
        var package = PackageReferences(project).SingleOrDefault(package => package.Include == include);
        Assert.NotNull(package);
        Assert.Equal(version, package!.Version);
    }

    private static IReadOnlyList<PackageReference> PackageReferences(XDocument project) =>
        project.Descendants("PackageReference")
            .Select(element => new PackageReference(
                element.Attribute("Include")?.Value ?? string.Empty,
                element.Attribute("Version")?.Value ?? string.Empty))
            .ToList();

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static void AssertSafeStaffText(string? value, params string?[] forbiddenValues)
    {
        var text = value ?? string.Empty;
        string[] forbiddenFragments = ["MinIO", "minio", "artifact-images/", "AccessKey", "SecretKey", "credential", "C:\\", "/data/", "Exception", "Connection refused"];
        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(fragment, text, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var forbiddenValue in forbiddenValues.Where(static item => !string.IsNullOrWhiteSpace(item)))
        {
            Assert.DoesNotContain(forbiddenValue!, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class StaticPermissionChecker(IReadOnlyCollection<string> permissions) : ICurrentActorPermissionChecker
    {
        private readonly HashSet<string> permissions = new(permissions, StringComparer.Ordinal);

        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
    }

    private sealed class TestAuditActorContext(string userId) : IAuditActorContext
    {
        public AuditActor CurrentActor => new(userId, userId, true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class ThrowingAuditWriter : IAuditWriter
    {
        public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Simulated audit write failure.");
    }

    private sealed class FailingDeleteStorage(IArtifactImageStorage inner) : IArtifactImageStorage
    {
        public List<ImageStorageObjectKey> FailedKeys { get; } = [];

        public ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, string? checksum, CancellationToken cancellationToken = default) =>
            inner.StoreOriginalAsync(objectKey, content, contentType, lengthBytes, checksum, cancellationToken);

        public ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, ImageDerivativeKind derivativeKind, string? checksum, CancellationToken cancellationToken = default) =>
            inner.StoreDerivativeAsync(objectKey, content, contentType, lengthBytes, derivativeKind, checksum, cancellationToken);

        public ValueTask<ArtifactImageStorageStatResult> StatAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
            inner.StatAsync(objectKey, cancellationToken);

        public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
            inner.OpenReadAsync(objectKey, cancellationToken);

        public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(ImageStorageObjectKey objectKey, TimeSpan requestedLifetime, CancellationToken cancellationToken = default) =>
            inner.CreateShortLivedReadAccessAsync(objectKey, requestedLifetime, cancellationToken);

        public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
            inner.DeleteObjectAsync(objectKey, cancellationToken);

        public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys, CancellationToken cancellationToken = default)
        {
            FailedKeys.Add(originalObjectKey);
            FailedKeys.AddRange(derivativeObjectKeys);
            var objectResults = new[] { originalObjectKey }
                .Concat(derivativeObjectKeys)
                .Select(key => ArtifactImageStorageDeleteResult.Failed(key, ArtifactImageStorageResultKind.RetryableFailure, "Storage.InjectedDeleteFailure", "Image storage is currently unavailable."))
                .ToArray();

            return ValueTask.FromResult(ArtifactImageObjectsDeleteResult.PartialFailure(
                objectResults,
                "Storage.InjectedDeleteFailure",
                "Image deletion could not be completed. The operation was saved for internal processing."));
        }
    }

    private sealed record UploadSeed(Guid ArtifactId, Guid SetId, Guid ImageId);

    private sealed record TwoImageUploadSeed(Guid ArtifactId, Guid SetId, Guid ImageAId, Guid ImageBId);

    private sealed record PackageReference(string Include, string Version);

    private sealed record Feature001Snapshot(
        Guid ArtifactId,
        string MuseumNumberDisplay,
        Guid CategoryId,
        ArtifactCurrentStatus CurrentStatus,
        Guid? CurrentLocationId,
        string? CurrentHolderType,
        string? CurrentHolderName,
        Guid? LastKnownStorageLocationId,
        IReadOnlyList<MovementSnapshot> Movements,
        int ArtifactRowsByArtifactId,
        int ArtifactRowsByMuseumNumberDisplay);

    private sealed record MovementSnapshot(
        Guid MovementId,
        MovementType MovementType,
        Guid MovementGroupId,
        Guid ArtifactId,
        MovementRecipientType? RecipientType,
        string? RecipientName,
        string? Purpose,
        Guid? ReturnLocationId,
        string? Note,
        DateTimeOffset OccurredAt,
        string? RecordedBy);
}

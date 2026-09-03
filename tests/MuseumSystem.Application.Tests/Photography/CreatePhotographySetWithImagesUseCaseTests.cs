using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Common.Audit;
using Microsoft.EntityFrameworkCore.Storage;

using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class CreatePhotographySetWithImagesUseCaseTests
{
    [Fact]
    public async Task All_valid_files_create_set_images_derivatives_and_completed_operation()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            File(0, "front.jpg", [1, 2, 3]),
            File(1, "side.png", [4, 5, 6])
        ]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        Assert.NotNull(result.Value.PhotographySetId);
        Assert.Equal(2, result.Value.FileResults.Count);
        Assert.All(result.Value.FileResults, file => Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, file.Status));
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        Assert.Equal(2, await db.ArtifactImages.CountAsync());
        Assert.Equal(4, await db.ArtifactImageDerivatives.CountAsync());
        Assert.Equal(2, storage.StoreOriginalCalls.Count);
        Assert.Equal(4, storage.StoreDerivativeCalls.Count);
        Assert.DoesNotContain(result.Value.FileResults, file => file.StaffFacingMessage.Contains("artifact-images", StringComparison.OrdinalIgnoreCase));
        Assert.All(storage.StoreOriginalCalls, call => Assert.True(call.Bytes.Length > 0));
    }

    [Fact]
    public async Task Mixed_valid_and_rejected_files_persist_file_level_partial_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var processor = new FakeArtifactImageProcessor();
        processor.Reject("bad.txt", "Unsupported file type.");
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, processor, storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            File(0, "front.jpg", [1, 2, 3]),
            File(1, "bad.txt", [9, 9, 9])
        ]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, result.Value!.Status);
        Assert.Collection(result.Value.FileResults.OrderBy(file => file.ClientFileOrdinal),
            file => Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, file.Status),
            file =>
            {
                Assert.Equal(PhotographyUploadFileOutcomeStatus.Rejected, file.Status);
                Assert.Null(file.ArtifactImageId);
                Assert.Equal("Unsupported file type.", file.StaffFacingMessage);
            });
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        Assert.Single(storage.StoreOriginalCalls);
    }

    [Fact]
    public async Task Later_storage_failure_does_not_roll_back_earlier_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        storage.QueueOriginalWriteFailure(ArtifactImageStorageResultKind.RetryableFailure, "Storage temporarily unavailable.", callOrdinal: 1);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            File(0, "front.jpg", [1, 2, 3]),
            File(1, "side.jpg", [4, 5, 6])
        ]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, result.Value!.Status);
        Assert.Equal([PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Failed], result.Value.FileResults.OrderBy(file => file.ClientFileOrdinal).Select(file => file.Status).ToArray());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        Assert.Equal(2, storage.StoreOriginalCalls.Count);
    }

    [Fact]
    public async Task All_invalid_files_persist_failed_operation_without_usable_set_or_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var processor = new FakeArtifactImageProcessor();
        processor.Reject("bad-1.txt", "Unsupported file type.");
        processor.Reject("bad-2.gif", "Unsupported file type.");
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, processor, storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId,
        [
            File(0, "bad-1.txt", [1]),
            File(1, "bad-2.gif", [2])
        ]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
        Assert.Null(result.Value.PhotographySetId);
        Assert.Null(result.Value.PhotographySet);
        Assert.Equal(0, await db.PhotographySets.CountAsync());
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
        Assert.Empty(storage.StoreOriginalCalls);
    }

    [Fact]
    public async Task Missing_artifact_is_rejected_before_processor_or_storage_side_effects()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var processor = new FakeArtifactImageProcessor();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, processor, storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(Guid.NewGuid(), [File(0, "front.jpg", [1, 2, 3])]));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Artifact.NotFound");
        Assert.Equal(0, processor.ValidateCalls);
        Assert.Empty(storage.StoreOriginalCalls);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
    }

    [Fact]
    public async Task Trusted_actor_is_persisted_as_upload_operation_actor()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, actorUserId: " trusted-user ");

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(
            artifact.ArtifactId,
            [File(0, "front.jpg", [1, 2, 3])],
            photographerUserId: "photographer-business-value"));

        Assert.True(result.Succeeded);
        Assert.Equal("trusted-user", (await db.PhotographyUploadOperations.SingleAsync()).ActorUserId);
    }

    [Fact]
    public async Task Trusted_actor_is_persisted_as_image_uploader()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, actorUserId: " trusted-user ");

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(
            artifact.ArtifactId,
            [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal("trusted-user", (await db.ArtifactImages.SingleAsync()).UploadedByUserId);
    }

    [Fact]
    public async Task Trusted_actor_is_set_creator_while_photographer_user_id_stays_business_metadata()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, actorUserId: " trusted-user ");

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(
            artifact.ArtifactId,
            [File(0, "front.jpg", [1, 2, 3])],
            photographerUserId: "photographer-business-value"));

        Assert.True(result.Succeeded);
        var set = await db.PhotographySets.SingleAsync();
        Assert.Equal("trusted-user", set.CreatedByUserId);
        Assert.Equal("photographer-business-value", set.PhotographerUserId);
    }

    [Fact]
    public async Task Unauthenticated_actor_is_rejected_before_processor_storage_or_persistence_side_effects()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var processor = new FakeArtifactImageProcessor();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, processor, storage, actorUserId: " ");

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(
            artifact.ArtifactId,
            [File(0, "front.jpg", [1, 2, 3])]));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.ActorRequired");
        Assert.Equal(0, processor.ValidateCalls);
        Assert.Equal(0, processor.GenerateDerivativeCalls);
        Assert.Empty(storage.StoreOriginalCalls);
        Assert.Empty(storage.StoreDerivativeCalls);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(0, await db.PhotographySets.CountAsync());
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Append_uses_trusted_actor_for_operation_scope_and_uploaded_images()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyUploadApplicationTestHost.AddPhotographySet(db, artifact);
        await db.SaveChangesAsync();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, actorUserId: " trusted-user ");

        var result = await host.AppendUseCase.AppendImagesToPhotographySet(AppendCommand(
            set.PhotographySetId,
            [File(0, "append.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal("trusted-user", (await db.PhotographyUploadOperations.SingleAsync()).ActorUserId);
        Assert.Equal("trusted-user", (await db.ArtifactImages.SingleAsync()).UploadedByUserId);
    }

    [Fact]
    public async Task Append_rejects_unauthenticated_actor_before_set_lookup_processor_storage_or_persistence_side_effects()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var processor = new FakeArtifactImageProcessor();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, processor, storage, actorUserId: " ");

        var result = await host.AppendUseCase.AppendImagesToPhotographySet(AppendCommand(
            Guid.NewGuid(),
            [File(0, "append.jpg", [1, 2, 3])]));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.ActorRequired");
        Assert.Equal(0, processor.ValidateCalls);
        Assert.Equal(0, processor.GenerateDerivativeCalls);
        Assert.Empty(storage.StoreOriginalCalls);
        Assert.Empty(storage.StoreDerivativeCalls);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(0, await db.PhotographySets.CountAsync());
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public void Upload_commands_do_not_accept_caller_supplied_actor_or_permissions()
    {
        var forbiddenMemberNames = new[]
        {
            "Actor",
            "ActorUserId",
            "UploadedByUserId",
            "CreatedByUserId",
            string.Concat("Permission", "s"),
            "Roles"
        };

        AssertCommandShapeDoesNotExposeForbiddenIdentityInputs<CreatePhotographySetWithImagesCommand>(forbiddenMemberNames);
        AssertCommandShapeDoesNotExposeForbiddenIdentityInputs<AppendImagesToPhotographySetCommand>(forbiddenMemberNames);
    }

    [Fact]
    public async Task Exact_set_context_is_persisted_without_custody_movement_or_location_changes()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");
        var beforeStatus = artifact.CurrentStatus;
        var beforeLocationId = artifact.CurrentLocationId;
        var beforeHolderType = artifact.CurrentHolderType;
        var beforeHolderName = artifact.CurrentHolderName;
        await db.SaveChangesAsync();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(
            artifact.ArtifactId,
            [File(0, "during.jpg", [1, 2, 3])],
            purpose: PhotographyPurpose.DuringMaintenance,
            photographyDate: new DateOnly(2026, 8, 26),
            photographerUserId: " photographer-a "));

        Assert.True(result.Succeeded);
        var set = await db.PhotographySets.SingleAsync();
        Assert.Equal(artifact.ArtifactId, set.ArtifactId);
        Assert.Equal(PhotographyPurpose.DuringMaintenance, set.Purpose);
        Assert.Equal(new DateOnly(2026, 8, 26), set.PhotographyDate);
        Assert.Equal("photographer-a", set.PhotographerUserId);
        Assert.Equal(beforeStatus, artifact.CurrentStatus);
        Assert.Equal(beforeLocationId, artifact.CurrentLocationId);
        Assert.Equal(beforeHolderType, artifact.CurrentHolderType);
        Assert.Equal(beforeHolderName, artifact.CurrentHolderName);
    }

    [Fact]
    public async Task Failed_cleanup_records_recovery_needed_without_staff_storage_internals()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage { CleanupSucceeds = false };
        storage.QueueDerivativeWriteFailure(ArtifactImageStorageResultKind.RetryableFailure, "Derivative storage failed.", callOrdinal: 0);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, Assert.Single(result.Value.FileResults).Status);
        Assert.Equal(1, await db.StorageOperationRecoveries.CountAsync());
        Assert.DoesNotContain("artifact-images", Assert.Single(result.Value.FileResults).StaffFacingMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Original_retryable_failure_after_store_is_verified_by_stat_and_continues()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        storage.QueueOriginalWriteFailureAfterStoring(ArtifactImageStorageResultKind.RetryableFailure, "Storage temporarily unavailable.", callOrdinal: 0);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, Assert.Single(result.Value.FileResults).Status);
        Assert.Single(storage.StoreOriginalCalls);
        Assert.Contains(storage.StoreOriginalCalls.Single().ObjectKey, storage.StatCalls);
        Assert.Empty(storage.DeleteImageObjectCalls);
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
    }

    [Fact]
    public async Task Original_retryable_failure_without_stored_object_becomes_failed_without_image_or_set()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        storage.QueueOriginalWriteFailure(ArtifactImageStorageResultKind.RetryableFailure, "Storage temporarily unavailable.", callOrdinal: 0);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, Assert.Single(result.Value.FileResults).Status);
        Assert.Contains(storage.StoreOriginalCalls.Single().ObjectKey, storage.StatCalls);
        Assert.Empty(storage.DeleteImageObjectCalls);
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
        Assert.Equal(0, await db.PhotographySets.CountAsync());
    }

    [Fact]
    public async Task Original_ambiguous_write_with_unknown_stat_records_recovery_for_attempted_key()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage { CleanupSucceeds = false };
        storage.QueueOriginalWriteFailure(ArtifactImageStorageResultKind.RetryableFailure, "Storage temporarily unavailable.", callOrdinal: 0);
        storage.QueueNextStatFailure(ArtifactImageStorageResultKind.RetryableFailure, "Stored object state could not be checked.");
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, Assert.Single(result.Value.FileResults).Status);
        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal([storage.StoreOriginalCalls.Single().ObjectKey], recovery.ObjectKeys);
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
        Assert.Equal(0, await db.PhotographySets.CountAsync());
    }

    [Fact]
    public async Task Original_already_exists_for_compatible_key_is_verified_by_stat_and_continues()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        storage.QueueOriginalWriteFailureAfterStoring(ArtifactImageStorageResultKind.AlreadyExists, "Object already exists.", callOrdinal: 0);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, Assert.Single(result.Value.FileResults).Status);
        Assert.Contains(storage.StoreOriginalCalls.Single().ObjectKey, storage.StatCalls);
        Assert.Equal(storage.StoreOriginalCalls.Single().ObjectKey.Value, (await db.PhotographyUploadFileOutcomes.SingleAsync()).OriginalObjectKey!.Value);
        Assert.Empty(storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Derivative_retryable_failure_after_store_is_verified_by_stat_and_continues()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        storage.QueueDerivativeWriteFailureAfterStoring(ArtifactImageStorageResultKind.RetryableFailure, "Derivative storage temporarily unavailable.", callOrdinal: 0);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, Assert.Single(result.Value.FileResults).Status);
        Assert.Contains(storage.StoreDerivativeCalls[0].ObjectKey, storage.StatCalls);
        Assert.Equal(2, await db.ArtifactImageDerivatives.CountAsync());
        Assert.Empty(storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Derivative_already_exists_for_compatible_key_is_verified_by_stat_and_continues()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        storage.QueueDerivativeWriteFailureAfterStoring(ArtifactImageStorageResultKind.AlreadyExists, "Derivative already exists.", callOrdinal: 0);
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Succeeded, Assert.Single(result.Value.FileResults).Status);
        Assert.Contains(storage.StoreDerivativeCalls[0].ObjectKey, storage.StatCalls);
        Assert.Equal(2, await db.ArtifactImageDerivatives.CountAsync());
        Assert.Empty(storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Derivative_ambiguous_unknown_state_cleanup_includes_current_attempted_key()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        storage.QueueDerivativeWriteFailure(ArtifactImageStorageResultKind.RetryableFailure, "Derivative storage temporarily unavailable.", callOrdinal: 1);
        storage.QueueNextStatFailure(ArtifactImageStorageResultKind.RetryableFailure, "Derivative state could not be checked.");
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, Assert.Single(result.Value.FileResults).Status);
        var cleanupKeys = Assert.Single(storage.DeleteImageObjectCalls);
        Assert.Equal([
            storage.StoreOriginalCalls.Single().ObjectKey,
            storage.StoreDerivativeCalls[0].ObjectKey,
            storage.StoreDerivativeCalls[1].ObjectKey
        ], cleanupKeys);
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Partial_cleanup_after_ambiguous_derivative_records_recovery_with_all_possible_keys()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage { CleanupSucceeds = false };
        storage.QueueDerivativeWriteFailure(ArtifactImageStorageResultKind.RetryableFailure, "Derivative storage temporarily unavailable.", callOrdinal: 1);
        storage.QueueNextStatFailure(ArtifactImageStorageResultKind.RetryableFailure, "Derivative state could not be checked.");
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, result.Value!.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, Assert.Single(result.Value.FileResults).Status);
        var expectedKeys = new[]
        {
            storage.StoreOriginalCalls.Single().ObjectKey,
            storage.StoreDerivativeCalls[0].ObjectKey,
            storage.StoreDerivativeCalls[1].ObjectKey
        };
        Assert.Equal(expectedKeys, Assert.Single(storage.DeleteImageObjectCalls));
        Assert.Equal(expectedKeys, (await db.StorageOperationRecoveries.SingleAsync()).ObjectKeys);
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Provider_operational_summary_never_leaks_to_upload_result_messages()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage { CleanupSucceeds = false };
        storage.QueueOriginalWriteFailure(ArtifactImageStorageResultKind.RetryableFailure, "Storage temporarily unavailable.", callOrdinal: 0);
        storage.QueueNextStatFailure(ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured, "Storage state could not be checked.");
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(artifact.ArtifactId, [File(0, "front.jpg", [1, 2, 3])]));

        Assert.True(result.Succeeded);
        var fileResult = Assert.Single(result.Value!.FileResults);
        AssertNoStaffStorageLeak(fileResult.StaffFacingMessage);
        AssertNoStaffStorageLeak(result.Value.PhotographySet?.PhotographySetId.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task Successful_metadata_failure_with_cleanup_success_records_failed_outcome_without_ghost_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var faultingContext = new FaultingMuseumDbContext(db) { ThrowNextSuccessfulFileMetadataSave = true };
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: faultingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(
            artifact.ArtifactId,
            [File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "metadata-failure-cleanup-success-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, result.Value!.Status);
        Assert.Single(storage.StoreOriginalCalls);
        Assert.Equal(2, storage.StoreDerivativeCalls.Count);
        var expectedKeys = new[]
        {
            storage.StoreOriginalCalls.Single().ObjectKey,
            storage.StoreDerivativeCalls[0].ObjectKey,
            storage.StoreDerivativeCalls[1].ObjectKey
        };
        Assert.Equal(expectedKeys, Assert.Single(storage.DeleteImageObjectCalls));

        var operation = await db.PhotographyUploadOperations
            .Include(uploadOperation => uploadOperation.FileOutcomes)
            .SingleAsync();
        var outcome = Assert.Single(operation.FileOutcomes);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, operation.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Failed, outcome.Status);
        Assert.DoesNotContain(operation.FileOutcomes, fileOutcome => fileOutcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
        Assert.Equal(1, operation.FileOutcomes.Count(fileOutcome => fileOutcome.ClientFileOrdinal == 0));
        Assert.Equal(1, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(0, await db.PhotographySets.CountAsync());
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
        Assert.Equal(0, await db.ArtifactImageDerivatives.CountAsync());
        Assert.Equal(0, await db.StorageOperationRecoveries.CountAsync());
        Assert.Equal(1, faultingContext.SuccessfulFileMetadataFailuresThrown);
        Assert.True(faultingContext.ClearTrackedChangesCalls > 0);
        AssertNoRolledBackSuccessfulMetadataEntries(db);
    }

    [Fact]
    public async Task Successful_metadata_failure_with_cleanup_failure_records_recovery_without_ghost_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var faultingContext = new FaultingMuseumDbContext(db) { ThrowNextSuccessfulFileMetadataSave = true };
        var storage = new FakeArtifactImageStorage { CleanupSucceeds = false };
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: faultingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreateCommand(
            artifact.ArtifactId,
            [File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "metadata-failure-cleanup-failure-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, result.Value!.Status);
        Assert.Single(storage.StoreOriginalCalls);
        Assert.Equal(2, storage.StoreDerivativeCalls.Count);
        var expectedKeys = new[]
        {
            storage.StoreOriginalCalls.Single().ObjectKey,
            storage.StoreDerivativeCalls[0].ObjectKey,
            storage.StoreDerivativeCalls[1].ObjectKey
        };
        Assert.Equal(expectedKeys, Assert.Single(storage.DeleteImageObjectCalls));

        var operation = await db.PhotographyUploadOperations
            .Include(uploadOperation => uploadOperation.FileOutcomes)
            .SingleAsync();
        var outcome = Assert.Single(operation.FileOutcomes);
        Assert.Equal(PhotographyUploadOperationStatus.RecoveryNeeded, operation.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.RecoveryNeeded, outcome.Status);
        Assert.DoesNotContain(operation.FileOutcomes, fileOutcome => fileOutcome.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
        Assert.Equal(1, operation.FileOutcomes.Count(fileOutcome => fileOutcome.ClientFileOrdinal == 0));
        Assert.Equal(expectedKeys[0], outcome.OriginalObjectKey);
        Assert.Equal(expectedKeys.Skip(1), outcome.DerivativeObjectKeys);
        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(expectedKeys, recovery.ObjectKeys);
        Assert.Equal(1, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(0, await db.PhotographySets.CountAsync());
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
        Assert.Equal(0, await db.ArtifactImageDerivatives.CountAsync());
        Assert.Equal(1, faultingContext.SuccessfulFileMetadataFailuresThrown);
        Assert.True(faultingContext.ClearTrackedChangesCalls > 0);
        AssertNoRolledBackSuccessfulMetadataEntries(db);
    }
    private static void AssertNoRolledBackSuccessfulMetadataEntries(MuseumDbContext db)
    {
        Assert.DoesNotContain(db.ChangeTracker.Entries<PhotographySet>(), entry => IsAddedOrModified(entry.State));
        Assert.DoesNotContain(db.ChangeTracker.Entries<ArtifactImage>(), entry => IsAddedOrModified(entry.State));
        Assert.DoesNotContain(db.ChangeTracker.Entries<ArtifactImageDerivative>(), entry => IsAddedOrModified(entry.State));
        Assert.DoesNotContain(db.ChangeTracker.Entries<PhotographyUploadFileOutcome>(), entry =>
            IsAddedOrModified(entry.State) && entry.Entity.Status == PhotographyUploadFileOutcomeStatus.Succeeded);

        static bool IsAddedOrModified(EntityState state) => state is EntityState.Added or EntityState.Modified;
    }

    internal static CreatePhotographySetWithImagesCommand CreateCommand(
        Guid artifactId,
        IReadOnlyList<PhotographyUploadFileInput> files,
        string idempotencyKey = "upload-key-1",
        PhotographyPurpose purpose = PhotographyPurpose.GeneralDocumentation,
        DateOnly? photographyDate = null,
        string photographerUserId = "photographer-1") =>
        new(
            artifactId,
            purpose,
            photographyDate ?? new DateOnly(2026, 8, 25),
            photographerUserId,
            idempotencyKey,
            files);

    internal static AppendImagesToPhotographySetCommand AppendCommand(
        Guid photographySetId,
        IReadOnlyList<PhotographyUploadFileInput> files,
        string idempotencyKey = "append-key-1",
        Guid? artifactConfirmation = null,
        PhotographyPurpose? purposeConfirmation = null) =>
        new(photographySetId, idempotencyKey, files, artifactConfirmation, purposeConfirmation);

    internal static PhotographyUploadFileInput File(int ordinal, string filename, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new PhotographyUploadFileInput(ordinal, filename, stream, bytes.LongLength);
    }

    private static void AssertNoStaffStorageLeak(string value)
    {
        Assert.DoesNotContain("provider://", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifact-images", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", value, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertCommandShapeDoesNotExposeForbiddenIdentityInputs<TCommand>(IReadOnlyCollection<string> forbiddenMemberNames)
    {
        var members = typeof(TCommand)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.Name))
            .Concat(typeof(TCommand).GetProperties().Select(property => property.Name))
            .Where(name => name is not null)
            .ToArray();

        foreach (var forbiddenName in forbiddenMemberNames)
        {
            Assert.DoesNotContain(forbiddenName, members);
        }
    }
}

internal static class PhotographyUploadApplicationTestHost
{
    public static MuseumDbContext CreateDbContext() =>
        CreateDbContext(new InMemoryDatabaseRoot(), Guid.NewGuid().ToString());

    public static MuseumDbContext CreateDbContext(InMemoryDatabaseRoot root, string databaseName)
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(databaseName, root)
            .Options;

        return new MuseumDbContext(options);
    }

    public static Artifact AddArtifact(MuseumDbContext db)
    {
        var category = ArtifactCategory.Create("CER", "Ceramics");
        var location = Location.Create("Main storage", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Artifact", location);
        db.ArtifactCategories.Add(category);
        db.Locations.Add(location);
        db.Artifacts.Add(artifact);
        return artifact;
    }

    public static PhotographySet AddPhotographySet(MuseumDbContext db, Artifact artifact, PhotographyPurpose purpose = PhotographyPurpose.GeneralDocumentation)
    {
        var set = PhotographySet.Create(artifact.ArtifactId, purpose, new DateOnly(2026, 8, 20), "existing-photographer", "creator-1");
        db.PhotographySets.Add(set);
        return set;
    }

    public static PhotographyUploadUseCaseHost CreateUseCases(
        MuseumDbContext db,
        FakeArtifactImageProcessor? processor = null,
        FakeArtifactImageStorage? storage = null,
        RecordingUploadAuditWriter? audit = null,
        string actorUserId = "photographer-1",
        IMuseumDbContext? persistenceContext = null)
    {
        processor ??= new FakeArtifactImageProcessor();
        storage ??= new FakeArtifactImageStorage();
        audit ??= new RecordingUploadAuditWriter();
        var persistence = new PhotographyUploadPersistenceService(persistenceContext ?? db);
        var fingerprint = new PhotographyUploadFingerprintService();
        var objectKeys = new PhotographyObjectKeyFactory();
        var auditService = new PhotographyUploadAuditService(audit);
        var consistency = new PhotographyUploadConsistencyService(persistence, storage, objectKeys, auditService);
        var mapper = new PhotographyResponseMapper();
        var actorContext = new TestAuditActorContext(actorUserId);
        return new PhotographyUploadUseCaseHost(
            new CreatePhotographySetWithImagesUseCase(persistence, processor, fingerprint, consistency, mapper, actorContext),
            new AppendImagesToPhotographySetUseCase(persistence, processor, fingerprint, consistency, mapper, actorContext),
            processor,
            storage,
            audit);
    }
}

internal sealed record PhotographyUploadUseCaseHost(
    CreatePhotographySetWithImagesUseCase CreateUseCase,
    AppendImagesToPhotographySetUseCase AppendUseCase,
    FakeArtifactImageProcessor Processor,
    FakeArtifactImageStorage Storage,
    RecordingUploadAuditWriter Audit);

internal sealed class FakeArtifactImageProcessor : IArtifactImageProcessor
{
    private readonly Dictionary<string, ArtifactImageValidationResult> validationByFilename = new(StringComparer.OrdinalIgnoreCase);

    public int ValidateCalls { get; private set; }
    public int GenerateDerivativeCalls { get; private set; }

    public void Reject(string filename, string message) =>
        validationByFilename[filename] = ArtifactImageValidationResult.Rejected("Unsupported", message);

    public void FailValidation(string filename, string message) =>
        validationByFilename[filename] = ArtifactImageValidationResult.Failed(ArtifactImageProcessingFailureKind.Retryable, "ProcessingFailure", message);

    public async ValueTask<ArtifactImageValidationResult> ValidateAsync(Stream imageContent, string originalFilename, long lengthBytes, CancellationToken cancellationToken = default)
    {
        ValidateCalls++;
        await imageContent.CopyToAsync(Stream.Null, cancellationToken);
        if (validationByFilename.TryGetValue(originalFilename, out var configured))
        {
            return configured;
        }

        var format = originalFilename.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? ArtifactImageFormat.Png : ArtifactImageFormat.Jpeg;
        var contentType = format == ArtifactImageFormat.Png ? "image/png" : "image/jpeg";
        var extension = format == ArtifactImageFormat.Png ? ".png" : ".jpg";
        return ArtifactImageValidationResult.Valid(new ArtifactImageMediaDescriptor(format, contentType, extension, 800, 600, lengthBytes));
    }

    public async ValueTask<ArtifactImageDerivativeGenerationResult> GenerateDerivativesAsync(Stream originalContent, ArtifactImageMediaDescriptor sourceImage, CancellationToken cancellationToken = default)
    {
        GenerateDerivativeCalls++;
        await originalContent.CopyToAsync(Stream.Null, cancellationToken);
        return ArtifactImageDerivativeGenerationResult.Success(
        [
            new ArtifactImageDerivativeContent(ImageDerivativeKind.Thumbnail, new MemoryStream([1, 2]), sourceImage.ContentType, sourceImage.NormalizedExtension, 2, 120, 90),
            new ArtifactImageDerivativeContent(ImageDerivativeKind.Preview, new MemoryStream([3, 4, 5]), sourceImage.ContentType, sourceImage.NormalizedExtension, 3, 640, 480)
        ]);
    }
}

internal sealed class FakeArtifactImageStorage : IArtifactImageStorage
{
    private readonly Dictionary<string, StoredFakeObject> objects = new(StringComparer.Ordinal);
    private readonly Dictionary<int, QueuedStorageWrite> originalWrites = [];
    private readonly Dictionary<int, QueuedStorageWrite> derivativeWrites = [];
    private readonly Queue<ArtifactImageStorageStatResult> queuedStatFailures = [];

    public List<StoredObjectCall> StoreOriginalCalls { get; } = [];
    public List<StoredObjectCall> StoreDerivativeCalls { get; } = [];
    public List<ImageStorageObjectKey> StatCalls { get; } = [];
    public List<IReadOnlyCollection<ImageStorageObjectKey>> DeleteImageObjectCalls { get; } = [];
    public bool CleanupSucceeds { get; set; } = true;
    public HashSet<int> CleanupFailureIndexes { get; } = [];

    public void QueueOriginalWriteFailure(ArtifactImageStorageResultKind kind, string message, int callOrdinal) =>
        originalWrites[callOrdinal] = QueuedStorageWrite.BeforeStore(kind, "OriginalFailure", message, "provider://internal/original");

    public void QueueOriginalWriteFailureAfterStoring(ArtifactImageStorageResultKind kind, string message, int callOrdinal) =>
        originalWrites[callOrdinal] = QueuedStorageWrite.AfterStore(kind, "OriginalFailure", message, "provider://internal/original");

    public void QueueDerivativeWriteFailure(ArtifactImageStorageResultKind kind, string message, int callOrdinal) =>
        derivativeWrites[callOrdinal] = QueuedStorageWrite.BeforeStore(kind, "DerivativeFailure", message, "provider://internal/derivative");

    public void QueueDerivativeWriteFailureAfterStoring(ArtifactImageStorageResultKind kind, string message, int callOrdinal) =>
        derivativeWrites[callOrdinal] = QueuedStorageWrite.AfterStore(kind, "DerivativeFailure", message, "provider://internal/derivative");

    public void QueueNextStatFailure(ArtifactImageStorageResultKind kind, string message) =>
        queuedStatFailures.Enqueue(ArtifactImageStorageStatResult.Failed(kind, "StatFailure", message, "provider://internal/stat"));

    public async ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, string? checksum, CancellationToken cancellationToken = default)
    {
        var callOrdinal = StoreOriginalCalls.Count;
        if (originalWrites.TryGetValue(callOrdinal, out var queued) && !queued.StoreObject)
        {
            StoreOriginalCalls.Add(new StoredObjectCall(objectKey, []));
            return queued.ToResult();
        }

        var bytes = await ReadBytesAsync(content, cancellationToken);
        StoreOriginalCalls.Add(new StoredObjectCall(objectKey, bytes));
        StoreObject(objectKey, bytes, contentType, lengthBytes, checksum);
        return queued is not null
            ? queued.ToResult()
            : ArtifactImageStorageWriteResult.Success(MetadataFor(objectKey));
    }

    public async ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, ImageDerivativeKind derivativeKind, string? checksum, CancellationToken cancellationToken = default)
    {
        var callOrdinal = StoreDerivativeCalls.Count;
        if (derivativeWrites.TryGetValue(callOrdinal, out var queued) && !queued.StoreObject)
        {
            StoreDerivativeCalls.Add(new StoredObjectCall(objectKey, []));
            return queued.ToResult();
        }

        var bytes = await ReadBytesAsync(content, cancellationToken);
        StoreDerivativeCalls.Add(new StoredObjectCall(objectKey, bytes));
        StoreObject(objectKey, bytes, contentType, lengthBytes, checksum);
        return queued is not null
            ? queued.ToResult()
            : ArtifactImageStorageWriteResult.Success(MetadataFor(objectKey));
    }

    public ValueTask<ArtifactImageStorageStatResult> StatAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default)
    {
        StatCalls.Add(objectKey);
        if (queuedStatFailures.TryDequeue(out var failure))
        {
            return ValueTask.FromResult(failure);
        }

        return ValueTask.FromResult(objects.TryGetValue(objectKey.Value, out var stored)
            ? ArtifactImageStorageStatResult.Success(stored.Metadata)
            : ArtifactImageStorageStatResult.Failed(ArtifactImageStorageResultKind.NotFound, "NotFound", "Stored object was not found."));
    }

    public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(ImageStorageObjectKey objectKey, TimeSpan requestedLifetime, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default)
    {
        objects.Remove(objectKey.Value);
        return ValueTask.FromResult(ArtifactImageStorageDeleteResult.Success(objectKey));
    }

    public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(ImageStorageObjectKey originalObjectKey, IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys, CancellationToken cancellationToken = default)
    {
        var keys = new[] { originalObjectKey }.Concat(derivativeObjectKeys).ToArray();
        DeleteImageObjectCalls.Add(keys);
        if (!CleanupSucceeds)
        {
            var failureIndexes = CleanupFailureIndexes.Count == 0
                ? Enumerable.Range(0, keys.Length).ToHashSet()
                : CleanupFailureIndexes;
            var results = keys.Select((key, index) =>
            {
                if (failureIndexes.Contains(index))
                {
                    return ArtifactImageStorageDeleteResult.Failed(key, ArtifactImageStorageResultKind.RetryableFailure, "DeleteFailed", "Object cleanup failed.");
                }

                objects.Remove(key.Value);
                return ArtifactImageStorageDeleteResult.Success(key);
            }).ToArray();

            return ValueTask.FromResult(ArtifactImageObjectsDeleteResult.PartialFailure(
                results,
                "CleanupFailed",
                "Object cleanup failed.",
                "provider://internal/delete"));
        }

        foreach (var key in keys)
        {
            objects.Remove(key.Value);
        }

        return ValueTask.FromResult(ArtifactImageObjectsDeleteResult.Success(keys.Select(ArtifactImageStorageDeleteResult.Success).ToArray()));
    }

    private void StoreObject(ImageStorageObjectKey objectKey, byte[] bytes, string contentType, long lengthBytes, string? checksum) =>
        objects[objectKey.Value] = new StoredFakeObject(bytes, new ArtifactImageStoredObjectMetadata(objectKey, contentType, lengthBytes, checksum, DateTimeOffset.UtcNow));

    private ArtifactImageStoredObjectMetadata MetadataFor(ImageStorageObjectKey objectKey) =>
        objects[objectKey.Value].Metadata;

    private static async Task<byte[]> ReadBytesAsync(Stream content, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        return copy.ToArray();
    }
}

internal sealed record StoredObjectCall(ImageStorageObjectKey ObjectKey, byte[] Bytes);

internal sealed record StoredFakeObject(byte[] Bytes, ArtifactImageStoredObjectMetadata Metadata);

internal sealed record QueuedStorageWrite(
    bool StoreObject,
    ArtifactImageStorageResultKind Kind,
    string Code,
    string StaffFacingMessage,
    string OperationalSummary)
{
    public static QueuedStorageWrite BeforeStore(ArtifactImageStorageResultKind kind, string code, string staffFacingMessage, string operationalSummary) =>
        new(false, kind, code, staffFacingMessage, operationalSummary);

    public static QueuedStorageWrite AfterStore(ArtifactImageStorageResultKind kind, string code, string staffFacingMessage, string operationalSummary) =>
        new(true, kind, code, staffFacingMessage, operationalSummary);

    public ArtifactImageStorageWriteResult ToResult() =>
        ArtifactImageStorageWriteResult.Failed(Kind, Code, StaffFacingMessage, OperationalSummary);
}

internal sealed class RecordingUploadAuditWriter : IAuditWriter
{
    private int sequence;

    public List<AuditWriteRequest> Requests { get; } = [];

    public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        sequence++;
        return Task.FromResult($"audit-{sequence}");
    }
}

internal sealed class TestAuditActorContext(string? userId = "photographer-1", string displayName = "Photography User") : IAuditActorContext
{
    public AuditActor CurrentActor => string.IsNullOrWhiteSpace(userId)
        ? AuditActor.System
        : new AuditActor(userId, displayName, true);
}

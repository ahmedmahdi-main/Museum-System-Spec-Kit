using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class PhotographyUploadIdempotencyTests
{
    [Fact]
    public async Task Same_actor_kind_key_and_same_request_returns_authoritative_replay_without_duplicate_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var firstStorage = new FakeArtifactImageStorage();
        var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: firstStorage);
        var command = CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "same-key");

        var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(command);
        var firstOutcome = await db.PhotographyUploadFileOutcomes.SingleAsync();
        var firstImageId = firstOutcome.ArtifactImageId;
        var firstObjectKey = firstOutcome.OriginalObjectKey!.Value;
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: replayStorage);
        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "same-key"));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Value!.PhotographyUploadOperationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Equal(firstImageId, replay.Value.FileResults.Single().ArtifactImageId);
        Assert.Equal(firstObjectKey, (await db.PhotographyUploadFileOutcomes.SingleAsync()).OriginalObjectKey!.Value);
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
    }

    [Fact]
    public async Task Same_key_with_materially_different_request_conflicts_without_mutating_prior_operation()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage);
        var first = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "conflict-key"));

        var conflict = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [9, 9, 9])],
            idempotencyKey: "conflict-key"));

        Assert.True(first.Succeeded);
        Assert.False(conflict.Succeeded);
        Assert.True(conflict.ConcurrencyConflict);
        Assert.Single(storage.StoreOriginalCalls);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Same_key_under_different_actor_is_a_separate_idempotency_scope()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, actorUserId: "photographer-1");
        var secondHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, actorUserId: "photographer-2");

        var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1])],
            idempotencyKey: "actor-key"));
        var second = await secondHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1])],
            idempotencyKey: "actor-key"));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotEqual(first.Value!.PhotographyUploadOperationId, second.Value!.PhotographyUploadOperationId);
        Assert.Equal(2, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(2, await db.PhotographySets.CountAsync());
    }

    [Fact]
    public async Task Same_key_under_different_operation_kind_is_a_separate_idempotency_scope()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db);
        var create = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1])],
            idempotencyKey: "shared-kind-key"));

        var append = await host.AppendUseCase.AppendImagesToPhotographySet(CreatePhotographySetWithImagesUseCaseTests.AppendCommand(
            create.Value!.PhotographySetId!.Value,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "append.jpg", [2])],
            idempotencyKey: "shared-kind-key"));

        Assert.True(create.Succeeded);
        Assert.True(append.Succeeded);
        Assert.NotEqual(create.Value.PhotographyUploadOperationId, append.Value!.PhotographyUploadOperationId);
        Assert.Equal(2, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
        Assert.Equal(2, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Partial_success_and_rejected_outcomes_are_stable_on_restart_replay()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var processor = new FakeArtifactImageProcessor();
        processor.Reject("bad.txt", "Unsupported file type.");
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, processor, storage);
        var first = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "bad.txt", [9, 9])
            ],
            idempotencyKey: "partial-key"));
        var firstOutcomes = await db.PhotographyUploadFileOutcomes.OrderBy(outcome => outcome.ClientFileOrdinal).ToListAsync();
        var firstImageId = firstOutcomes[0].ArtifactImageId;
        var firstObjectKey = firstOutcomes[0].OriginalObjectKey!.Value;
        var firstRejectedOutcomeId = firstOutcomes[1].PhotographyUploadFileOutcomeId;

        var replayProcessor = new FakeArtifactImageProcessor();
        replayProcessor.Reject("bad.txt", "Unsupported file type.");
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, replayProcessor, replayStorage);
        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "bad.txt", [9, 9])
            ],
            idempotencyKey: "partial-key"));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, replay.Value!.Status);
        Assert.Equal([PhotographyUploadFileOutcomeStatus.Succeeded, PhotographyUploadFileOutcomeStatus.Rejected], replay.Value.FileResults.OrderBy(file => file.ClientFileOrdinal).Select(file => file.Status).ToArray());
        Assert.Equal(firstImageId, replay.Value.FileResults.Single(file => file.Status == PhotographyUploadFileOutcomeStatus.Succeeded).ArtifactImageId);
        Assert.Equal(firstObjectKey, (await db.PhotographyUploadFileOutcomes.SingleAsync(outcome => outcome.ClientFileOrdinal == 0)).OriginalObjectKey!.Value);
        Assert.Equal(firstRejectedOutcomeId, (await db.PhotographyUploadFileOutcomes.SingleAsync(outcome => outcome.ClientFileOrdinal == 1)).PhotographyUploadFileOutcomeId);
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Equal(1, await db.ArtifactImages.CountAsync());
        Assert.Equal(1, await db.PhotographySets.CountAsync());
    }

    [Fact]
    public async Task Same_raw_request_with_transient_validation_failure_then_success_replays_without_fingerprint_conflict()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var firstProcessor = new FakeArtifactImageProcessor();
        firstProcessor.FailValidation("front.jpg", "Image processing is temporarily unavailable.");
        var firstStorage = new FakeArtifactImageStorage();
        var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, firstProcessor, firstStorage);
        var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "stable-transient-failure-key"));
        var operationId = first.Value!.PhotographyUploadOperationId;
        var requestFingerprint = await db.PhotographyUploadOperations.Select(operation => operation.RequestFingerprint).SingleAsync();

        var secondProcessor = new FakeArtifactImageProcessor();
        var secondStorage = new FakeArtifactImageStorage();
        var secondHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, secondProcessor, secondStorage);
        var replay = await secondHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "stable-transient-failure-key"));

        Assert.True(first.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, first.Value.Status);
        Assert.True(replay.Succeeded);
        Assert.False(replay.ConcurrencyConflict);
        Assert.Equal(operationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, replay.Value.Status);
        Assert.Equal(requestFingerprint, await db.PhotographyUploadOperations.Select(operation => operation.RequestFingerprint).SingleAsync());
        Assert.Empty(firstStorage.StoreOriginalCalls);
        Assert.Empty(secondStorage.StoreOriginalCalls);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Same_raw_request_with_rejection_then_acceptance_replays_without_fingerprint_conflict()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var firstProcessor = new FakeArtifactImageProcessor();
        firstProcessor.Reject("front.jpg", "Unsupported file type.");
        var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, firstProcessor, new FakeArtifactImageStorage());
        var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "stable-rejection-key"));
        var rejectedOutcomeId = await db.PhotographyUploadFileOutcomes.Select(outcome => outcome.PhotographyUploadFileOutcomeId).SingleAsync();

        var acceptingStorage = new FakeArtifactImageStorage();
        var acceptingHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, new FakeArtifactImageProcessor(), acceptingStorage);
        var replay = await acceptingHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "stable-rejection-key"));

        Assert.True(first.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, first.Value!.Status);
        Assert.True(replay.Succeeded);
        Assert.False(replay.ConcurrencyConflict);
        Assert.Equal(first.Value.PhotographyUploadOperationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, replay.Value.Status);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Rejected, replay.Value.FileResults.Single().Status);
        Assert.Equal(rejectedOutcomeId, await db.PhotographyUploadFileOutcomes.Select(outcome => outcome.PhotographyUploadFileOutcomeId).SingleAsync());
        Assert.Empty(acceptingStorage.StoreOriginalCalls);
        Assert.Empty(acceptingStorage.StoreDerivativeCalls);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
    }

    [Fact]
    public async Task Same_raw_request_with_processor_outcome_change_replays_after_restart_without_conflict()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        Guid artifactId;
        Guid operationId;
        string requestFingerprint;
        MuseumDbContext? db1Instance;

        await using (var db1 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName))
        {
            db1Instance = db1;
            var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db1);
            await db1.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            var processor = new FakeArtifactImageProcessor();
            processor.FailValidation("front.jpg", "Image processing is temporarily unavailable.");
            var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db1, processor, new FakeArtifactImageStorage());
            var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
                artifactId,
                [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
                idempotencyKey: "stable-restart-outcome-key"));
            operationId = first.Value!.PhotographyUploadOperationId;
            requestFingerprint = await db1.PhotographyUploadOperations.Select(operation => operation.RequestFingerprint).SingleAsync();
        }

        await using var db2 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName);
        Assert.NotSame(db1Instance, db2);
        Assert.Empty(db2.ChangeTracker.Entries<PhotographyUploadOperation>());
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db2, new FakeArtifactImageProcessor(), replayStorage);

        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "stable-restart-outcome-key"));

        Assert.True(replay.Succeeded);
        Assert.False(replay.ConcurrencyConflict);
        Assert.Equal(operationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Equal(PhotographyUploadOperationStatus.Failed, replay.Value.Status);
        Assert.Equal(requestFingerprint, await db2.PhotographyUploadOperations.Select(operation => operation.RequestFingerprint).SingleAsync());
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Equal(1, await db2.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db2.PhotographyUploadFileOutcomes.CountAsync());
    }

    [Fact]
    public async Task Same_key_with_different_raw_bytes_still_conflicts_without_storage_mutation()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var firstProcessor = new FakeArtifactImageProcessor();
        firstProcessor.FailValidation("front.jpg", "Image processing is temporarily unavailable.");
        var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, firstProcessor, new FakeArtifactImageStorage());
        var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "different-bytes-conflict-key"));
        var storedFingerprint = await db.PhotographyUploadOperations.Select(operation => operation.RequestFingerprint).SingleAsync();
        var conflictStorage = new FakeArtifactImageStorage();
        var conflictHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: conflictStorage);

        var conflict = await conflictHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [9, 9, 9])],
            idempotencyKey: "different-bytes-conflict-key"));

        Assert.True(first.Succeeded);
        Assert.False(conflict.Succeeded);
        Assert.True(conflict.ConcurrencyConflict);
        Assert.Equal(storedFingerprint, await db.PhotographyUploadOperations.Select(operation => operation.RequestFingerprint).SingleAsync());
        Assert.Empty(conflictStorage.StoreOriginalCalls);
        Assert.Empty(conflictStorage.StoreDerivativeCalls);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db.PhotographyUploadFileOutcomes.CountAsync());
    }

    [Fact]
    public async Task Persisted_request_fingerprint_uses_raw_file_identity_not_processor_outcome()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var failedProcessor = new FakeArtifactImageProcessor();
        failedProcessor.FailValidation("front.jpg", "Image processing is temporarily unavailable.");
        var failedHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, failedProcessor, new FakeArtifactImageStorage());
        var failed = await failedHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "fingerprint-failed-key"));

        var successfulHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: new FakeArtifactImageStorage());
        var successful = await successfulHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "fingerprint-success-key"));
        var fingerprints = await db.PhotographyUploadOperations
            .OrderBy(operation => operation.IdempotencyKey)
            .Select(operation => operation.RequestFingerprint)
            .ToListAsync();

        Assert.True(failed.Succeeded);
        Assert.True(successful.Succeeded);
        Assert.Equal(2, fingerprints.Count);
        Assert.Equal(fingerprints[0], fingerprints[1]);
    }

    [Fact]
    public async Task Completed_operation_replays_after_restart_from_new_context_without_storage()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        Guid artifactId;
        Guid operationId;
        Guid setId;
        Guid imageId;
        Guid outcomeId;
        string originalObjectKey;
        IReadOnlyList<Guid> derivativeIds;
        MuseumDbContext? db1Instance;

        await using (var db1 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName))
        {
            db1Instance = db1;
            var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db1);
            await db1.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            var host1 = PhotographyUploadApplicationTestHost.CreateUseCases(db1, storage: new FakeArtifactImageStorage());
            var first = await host1.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
                artifactId,
                [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
                idempotencyKey: "restart-completed-key"));

            Assert.True(first.Succeeded);
            operationId = first.Value!.PhotographyUploadOperationId;
            setId = first.Value.PhotographySetId!.Value;
            var outcome = await db1.PhotographyUploadFileOutcomes.SingleAsync();
            outcomeId = outcome.PhotographyUploadFileOutcomeId;
            imageId = outcome.ArtifactImageId!.Value;
            originalObjectKey = outcome.OriginalObjectKey!.Value;
            derivativeIds = await db1.ArtifactImageDerivatives.Select(derivative => derivative.ArtifactImageDerivativeId).OrderBy(id => id).ToListAsync();
        }

        await using var db2 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName);
        Assert.NotSame(db1Instance, db2);
        Assert.Empty(db2.ChangeTracker.Entries<PhotographyUploadOperation>());
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db2, storage: replayStorage);

        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "restart-completed-key"));

        Assert.True(replay.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, replay.Value!.Status);
        Assert.Equal(operationId, replay.Value.PhotographyUploadOperationId);
        Assert.Equal(setId, replay.Value.PhotographySetId);
        Assert.Equal(imageId, replay.Value.FileResults.Single().ArtifactImageId);
        Assert.Equal(outcomeId, await db2.PhotographyUploadFileOutcomes.Select(outcome => outcome.PhotographyUploadFileOutcomeId).SingleAsync());
        Assert.Equal(originalObjectKey, (await db2.PhotographyUploadFileOutcomes.SingleAsync()).OriginalObjectKey!.Value);
        Assert.Equal(derivativeIds, await db2.ArtifactImageDerivatives.Select(derivative => derivative.ArtifactImageDerivativeId).OrderBy(id => id).ToListAsync());
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Empty(replayStorage.StoreDerivativeCalls);
        Assert.Equal(1, await db2.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db2.PhotographySets.CountAsync());
        Assert.Equal(1, await db2.ArtifactImages.CountAsync());
        Assert.Equal(2, await db2.ArtifactImageDerivatives.CountAsync());
    }

    [Fact]
    public async Task CompletedWithFailures_operation_replays_after_restart_with_stable_success_and_rejection()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        Guid artifactId;
        Guid operationId;
        Guid setId;
        Guid imageId;
        Guid succeededOutcomeId;
        Guid rejectedOutcomeId;
        string originalObjectKey;
        MuseumDbContext? db1Instance;

        await using (var db1 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName))
        {
            db1Instance = db1;
            var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db1);
            await db1.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            var processor = new FakeArtifactImageProcessor();
            processor.Reject("bad.txt", "Unsupported file type.");
            var host1 = PhotographyUploadApplicationTestHost.CreateUseCases(db1, processor, new FakeArtifactImageStorage());
            var first = await host1.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
                artifactId,
                [
                    CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                    CreatePhotographySetWithImagesUseCaseTests.File(1, "bad.txt", [9, 9])
                ],
                idempotencyKey: "restart-partial-key"));

            Assert.True(first.Succeeded);
            Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, first.Value!.Status);
            operationId = first.Value.PhotographyUploadOperationId;
            setId = first.Value.PhotographySetId!.Value;
            var outcomes = await db1.PhotographyUploadFileOutcomes.OrderBy(outcome => outcome.ClientFileOrdinal).ToListAsync();
            succeededOutcomeId = outcomes[0].PhotographyUploadFileOutcomeId;
            rejectedOutcomeId = outcomes[1].PhotographyUploadFileOutcomeId;
            imageId = outcomes[0].ArtifactImageId!.Value;
            originalObjectKey = outcomes[0].OriginalObjectKey!.Value;
        }

        await using var db2 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName);
        Assert.NotSame(db1Instance, db2);
        Assert.Empty(db2.ChangeTracker.Entries<PhotographyUploadOperation>());
        var replayProcessor = new FakeArtifactImageProcessor();
        replayProcessor.Reject("bad.txt", "Unsupported file type.");
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db2, replayProcessor, replayStorage);

        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifactId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "bad.txt", [9, 9])
            ],
            idempotencyKey: "restart-partial-key"));

        Assert.True(replay.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.CompletedWithFailures, replay.Value!.Status);
        Assert.Equal(operationId, replay.Value.PhotographyUploadOperationId);
        Assert.Equal(setId, replay.Value.PhotographySetId);
        var replayResults = replay.Value.FileResults.OrderBy(file => file.ClientFileOrdinal).ToArray();
        Assert.Equal(imageId, replayResults[0].ArtifactImageId);
        Assert.Equal(PhotographyUploadFileOutcomeStatus.Rejected, replayResults[1].Status);
        var outcomesAfterReplay = await db2.PhotographyUploadFileOutcomes.OrderBy(outcome => outcome.ClientFileOrdinal).ToListAsync();
        Assert.Equal(succeededOutcomeId, outcomesAfterReplay[0].PhotographyUploadFileOutcomeId);
        Assert.Equal(rejectedOutcomeId, outcomesAfterReplay[1].PhotographyUploadFileOutcomeId);
        Assert.Equal(originalObjectKey, outcomesAfterReplay[0].OriginalObjectKey!.Value);
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Empty(replayStorage.StoreDerivativeCalls);
        Assert.Equal(1, await db2.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db2.PhotographySets.CountAsync());
        Assert.Equal(1, await db2.ArtifactImages.CountAsync());
        Assert.Equal(2, await db2.PhotographyUploadFileOutcomes.CountAsync());
    }

    [Fact]
    public async Task InProgress_operation_partially_resumes_after_restart_using_only_missing_file()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        Guid artifactId;
        Guid operationId;
        Guid setId;
        Guid firstImageId;
        string firstOriginalObjectKey;
        MuseumDbContext? db1Instance;

        await using (var db1 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName))
        {
            db1Instance = db1;
            var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db1);
            await db1.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            var seed = await SeedInProgressUploadWithSucceededOrdinalsAsync(
                db1,
                artifactId,
                "restart-inprogress-key",
                [0],
                [
                    CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                    CreatePhotographySetWithImagesUseCaseTests.File(1, "side.jpg", [4, 5, 6])
                ]);
            operationId = seed.OperationId;
            setId = seed.SetId;
            firstImageId = seed.ImageIds.Single();
            firstOriginalObjectKey = seed.OriginalObjectKeys.Single();
            Assert.Equal(PhotographyUploadOperationStatus.InProgress, (await db1.PhotographyUploadOperations.SingleAsync()).Status);
            Assert.Single(await db1.PhotographyUploadFileOutcomes.ToListAsync());
        }

        await using var db2 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName);
        Assert.NotSame(db1Instance, db2);
        Assert.Empty(db2.ChangeTracker.Entries<PhotographyUploadOperation>());
        var resumeStorage = new FakeArtifactImageStorage();
        var resumeHost = PhotographyUploadApplicationTestHost.CreateUseCases(db2, storage: resumeStorage);

        var resumed = await resumeHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifactId,
            [
                CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3]),
                CreatePhotographySetWithImagesUseCaseTests.File(1, "side.jpg", [4, 5, 6])
            ],
            idempotencyKey: "restart-inprogress-key"));

        Assert.True(resumed.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, resumed.Value!.Status);
        Assert.Equal(operationId, resumed.Value.PhotographyUploadOperationId);
        Assert.Equal(setId, resumed.Value.PhotographySetId);
        Assert.Single(resumeStorage.StoreOriginalCalls);
        Assert.Equal(2, resumeStorage.StoreDerivativeCalls.Count);
        Assert.DoesNotContain(firstOriginalObjectKey, resumeStorage.StoreOriginalCalls.Select(call => call.ObjectKey.Value));
        Assert.Equal(1, await db2.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db2.PhotographySets.CountAsync());
        Assert.Equal(2, await db2.ArtifactImages.CountAsync());
        Assert.Equal(firstImageId, (await db2.PhotographyUploadFileOutcomes.SingleAsync(outcome => outcome.ClientFileOrdinal == 0)).ArtifactImageId);
        Assert.Equal(firstOriginalObjectKey, (await db2.PhotographyUploadFileOutcomes.SingleAsync(outcome => outcome.ClientFileOrdinal == 0)).OriginalObjectKey!.Value);
    }

    [Fact]
    public async Task InProgress_operation_with_all_outcomes_replays_after_restart_and_finalizes_without_storage()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        Guid artifactId;
        Guid operationId;
        Guid setId;
        Guid imageId;
        string originalObjectKey;
        MuseumDbContext? db1Instance;

        await using (var db1 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName))
        {
            db1Instance = db1;
            var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db1);
            await db1.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            var seed = await SeedInProgressUploadWithSucceededOrdinalsAsync(
                db1,
                artifactId,
                "restart-unfinalized-key",
                [0],
                [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])]);
            operationId = seed.OperationId;
            setId = seed.SetId;
            imageId = seed.ImageIds.Single();
            originalObjectKey = seed.OriginalObjectKeys.Single();
            Assert.Equal(PhotographyUploadOperationStatus.InProgress, (await db1.PhotographyUploadOperations.SingleAsync()).Status);
            Assert.Single(await db1.PhotographyUploadFileOutcomes.ToListAsync());
        }

        await using var db2 = PhotographyUploadApplicationTestHost.CreateDbContext(root, databaseName);
        Assert.NotSame(db1Instance, db2);
        Assert.Empty(db2.ChangeTracker.Entries<PhotographyUploadOperation>());
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db2, storage: replayStorage);

        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "restart-unfinalized-key"));

        Assert.True(replay.Succeeded);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, replay.Value!.Status);
        Assert.Equal(operationId, replay.Value.PhotographyUploadOperationId);
        Assert.Equal(setId, replay.Value.PhotographySetId);
        Assert.Equal(imageId, replay.Value.FileResults.Single().ArtifactImageId);
        Assert.Equal(originalObjectKey, (await db2.PhotographyUploadFileOutcomes.SingleAsync()).OriginalObjectKey!.Value);
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Empty(replayStorage.StoreDerivativeCalls);
        Assert.Equal(1, await db2.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, await db2.PhotographySets.CountAsync());
        Assert.Equal(1, await db2.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Competing_insert_loser_reloads_matching_winner_and_uses_same_operation_identity()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        Guid winnerId = default;
        var racingContext = new FaultingMuseumDbContext(db)
        {
            OnOperationInsertFailureAsync = (inner, attempted) =>
            {
                var winner = SeedCompetingOperation(inner, attempted, attempted.RequestFingerprint);
                winnerId = winner.PhotographyUploadOperationId;
                return Task.CompletedTask;
            }
        };
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: racingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "race-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(winnerId, result.Value!.PhotographyUploadOperationId);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value.Status);
        Assert.Single(storage.StoreOriginalCalls);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(1, racingContext.InsertFailuresThrown);
        Assert.True(racingContext.ClearTrackedChangesCalls > 0);
    }

    [Fact]
    public async Task Competing_insert_loser_reloads_conflicting_winner_and_returns_idempotency_conflict()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var racingContext = new FaultingMuseumDbContext(db)
        {
            OnOperationInsertFailureAsync = (inner, attempted) =>
            {
                SeedCompetingOperation(inner, attempted, "different-request-fingerprint");
                return Task.CompletedTask;
            }
        };
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: racingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "race-conflict-key"));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Empty(storage.StoreOriginalCalls);
        Assert.Equal(1, await db.PhotographyUploadOperations.CountAsync());
        Assert.Equal(0, await db.PhotographyUploadFileOutcomes.CountAsync());
        Assert.Equal(0, await db.ArtifactImages.CountAsync());
    }

    [Fact]
    public async Task Insert_failure_without_authoritative_winner_is_rethrown_and_clears_failed_insert()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var racingContext = new FaultingMuseumDbContext(db) { ThrowOperationInsertWithoutWinner = true };
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: racingContext);

        await Assert.ThrowsAsync<DbUpdateException>(() => host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "non-race-key")));

        Assert.Empty(storage.StoreOriginalCalls);
        Assert.Equal(0, await db.PhotographyUploadOperations.CountAsync());
        Assert.DoesNotContain(db.ChangeTracker.Entries<PhotographyUploadOperation>(), entry => entry.State == EntityState.Added);
        Assert.Equal(1, racingContext.InsertFailuresThrown);
        Assert.True(racingContext.ClearTrackedChangesCalls > 0);
    }

    [Fact]
    public async Task Concurrent_mark_seen_loss_does_not_break_terminal_replay()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var firstStorage = new FakeArtifactImageStorage();
        var firstHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: firstStorage);
        var command = CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "mark-seen-terminal-key");
        var first = await firstHost.CreateUseCase.CreatePhotographySetWithImages(command);
        var replayContext = new FaultingMuseumDbContext(db) { ThrowNextMarkSeenConcurrency = true };
        var replayStorage = new FakeArtifactImageStorage();
        var replayHost = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: replayStorage, persistenceContext: replayContext);

        var replay = await replayHost.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "mark-seen-terminal-key"));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.Value!.PhotographyUploadOperationId, replay.Value!.PhotographyUploadOperationId);
        Assert.Empty(replayStorage.StoreOriginalCalls);
        Assert.Equal(1, replayContext.MarkSeenConcurrencyFailuresThrown);
        Assert.True(replayContext.ClearTrackedChangesCalls > 0);
    }

    [Fact]
    public async Task Concurrent_mark_seen_loss_does_not_break_inprogress_resume()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        Guid winnerId = default;
        var racingContext = new FaultingMuseumDbContext(db)
        {
            ThrowNextMarkSeenConcurrency = true,
            OnOperationInsertFailureAsync = (inner, attempted) =>
            {
                var winner = SeedCompetingOperation(inner, attempted, attempted.RequestFingerprint);
                winnerId = winner.PhotographyUploadOperationId;
                return Task.CompletedTask;
            }
        };
        var storage = new FakeArtifactImageStorage();
        var host = PhotographyUploadApplicationTestHost.CreateUseCases(db, storage: storage, persistenceContext: racingContext);

        var result = await host.CreateUseCase.CreatePhotographySetWithImages(CreatePhotographySetWithImagesUseCaseTests.CreateCommand(
            artifact.ArtifactId,
            [CreatePhotographySetWithImagesUseCaseTests.File(0, "front.jpg", [1, 2, 3])],
            idempotencyKey: "mark-seen-inprogress-key"));

        Assert.True(result.Succeeded);
        Assert.Equal(winnerId, result.Value!.PhotographyUploadOperationId);
        Assert.Equal(PhotographyUploadOperationStatus.Completed, result.Value.Status);
        Assert.Single(storage.StoreOriginalCalls);
        Assert.Equal(1, racingContext.InsertFailuresThrown);
        Assert.Equal(1, racingContext.MarkSeenConcurrencyFailuresThrown);
    }

    private static async Task<SeededInProgressUpload> SeedInProgressUploadWithSucceededOrdinalsAsync(
        MuseumDbContext db,
        Guid artifactId,
        string idempotencyKey,
        IReadOnlyCollection<int> succeededOrdinals,
        IReadOnlyList<PhotographyUploadFileInput> files)
    {
        var fingerprint = new PhotographyUploadFingerprintService();
        var objectKeys = new PhotographyObjectKeyFactory();
        var persistence = new PhotographyUploadPersistenceService(db);
        var command = CreatePhotographySetWithImagesUseCaseTests.CreateCommand(artifactId, files, idempotencyKey: idempotencyKey);
        var fileFingerprints = new Dictionary<int, string>();
        var fingerprintFiles = new List<PhotographyUploadFingerprintFileInput>();
        foreach (var file in files.OrderBy(file => file.ClientFileOrdinal))
        {
            var fingerprintFile = new PhotographyUploadFingerprintFileInput(
                file.ClientFileOrdinal,
                file.LengthBytes,
                await ComputeContentHashAsync(file),
                "application/octet-stream",
                1,
                1,
                "raw-upload",
                ".upload",
                file.OriginalFilename);
            fingerprintFiles.Add(fingerprintFile);
            fileFingerprints[file.ClientFileOrdinal] = fingerprint.ComputeFileFingerprint(fingerprintFile);
        }

        var requestFingerprint = fingerprint.ComputeRequestFingerprint(new PhotographyUploadFingerprintInput(
            artifactId,
            PhotographyUploadOperationKind.CreateSetUpload,
            null,
            command.Purpose,
            command.PhotographyDate,
            command.PhotographerUserId,
            fingerprintFiles));
        var operation = await persistence.GetOrStartUploadOperationAsync(
            "photographer-1",
            PhotographyUploadOperationKind.CreateSetUpload,
            idempotencyKey,
            requestFingerprint,
            artifactId,
            null);
        PhotographySet? set = null;
        var imageIds = new List<Guid>();
        var originalObjectKeys = new List<string>();

        foreach (var ordinal in succeededOrdinals.Order())
        {
            var file = files.Single(input => input.ClientFileOrdinal == ordinal);
            var media = file.OriginalFilename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? new ArtifactImageMediaDescriptor(ArtifactImageFormat.Png, "image/png", ".png", 800, 600, file.LengthBytes)
                : new ArtifactImageMediaDescriptor(ArtifactImageFormat.Jpeg, "image/jpeg", ".jpg", 800, 600, file.LengthBytes);
            var keyInput = new PhotographyObjectKeyInput(operation.PhotographyUploadOperationId, ordinal, fileFingerprints[ordinal], media.NormalizedExtension);
            var originalKey = objectKeys.CreateOriginalKey(keyInput);
            var derivativeKeys = new[]
            {
                objectKeys.CreateDerivativeKey(keyInput, ImageDerivativeKind.Thumbnail, media.NormalizedExtension),
                objectKeys.CreateDerivativeKey(keyInput, ImageDerivativeKind.Preview, media.NormalizedExtension)
            };
            var setToCreate = set is null
                ? PhotographySet.Create(artifactId, command.Purpose, command.PhotographyDate, command.PhotographerUserId, "photographer-1")
                : null;
            set ??= setToCreate!;
            var image = ArtifactImage.Create(
                artifactId,
                set.PhotographySetId,
                originalKey,
                file.OriginalFilename,
                media.ContentType,
                media.LengthBytes,
                media.PixelWidth,
                media.PixelHeight,
                "photographer-1",
                DateTimeOffset.UtcNow);
            var derivatives = new[]
            {
                ArtifactImageDerivative.Create(image.ArtifactImageId, ImageDerivativeKind.Thumbnail, derivativeKeys[0], media.ContentType, 2, 120, 90),
                ArtifactImageDerivative.Create(image.ArtifactImageId, ImageDerivativeKind.Preview, derivativeKeys[1], media.ContentType, 3, 640, 480)
            };
            foreach (var derivative in derivatives)
            {
                image.AddDerivative(derivative);
            }

            var outcome = PhotographyUploadFileOutcome.Succeeded(
                operation.PhotographyUploadOperationId,
                ordinal,
                file.OriginalFilename,
                fileFingerprints[ordinal],
                image.ArtifactImageId,
                originalKey,
                derivativeKeys,
                "File uploaded.");
            set = await persistence.PersistSuccessfulFileAsync(operation.PhotographyUploadOperationId, setToCreate is null ? set : null, setToCreate, image, derivatives, outcome);
            imageIds.Add(image.ArtifactImageId);
            originalObjectKeys.Add(originalKey.Value);
            operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId);
        }

        return new SeededInProgressUpload(operation.PhotographyUploadOperationId, operation.PhotographySetId!.Value, imageIds, originalObjectKeys);
    }

    private static async Task<string> ComputeContentHashAsync(PhotographyUploadFileInput file)
    {
        var originalPosition = file.Content.CanSeek ? file.Content.Position : 0;
        if (file.Content.CanSeek)
        {
            file.Content.Position = 0;
        }

        var hash = await SHA256.HashDataAsync(file.Content);
        if (file.Content.CanSeek)
        {
            file.Content.Position = originalPosition;
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static PhotographyUploadOperation SeedCompetingOperation(
        MuseumDbContext db,
        PhotographyUploadOperation attempted,
        string requestFingerprint)
    {
        var winner = PhotographyUploadOperation.Start(
            attempted.ActorUserId,
            attempted.OperationKind,
            attempted.IdempotencyKey,
            requestFingerprint,
            attempted.ArtifactId,
            attempted.PhotographySetId);

        db.ChangeTracker.Clear();
        db.PhotographyUploadOperations.Add(winner);
        db.SaveChanges();
        return winner;
    }

    [Fact]
    public void Object_key_factory_uses_opaque_domain_identity_not_filenames_paths_or_provider_details()
    {
        var factory = new PhotographyObjectKeyFactory();
        var input = new PhotographyObjectKeyInput(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            3,
            "abcdef1234567890abcdef1234567890",
            ".jpg");

        var original = factory.CreateOriginalKey(input).Value;
        var thumbnail = factory.CreateDerivativeKey(input, ImageDerivativeKind.Thumbnail, ".jpg").Value;
        var preview = factory.CreateDerivativeKey(input, ImageDerivativeKind.Preview, ".jpg").Value;

        Assert.Equal(original, factory.CreateOriginalKey(input).Value);
        Assert.NotEqual(original, thumbnail);
        Assert.NotEqual(thumbnail, preview);
        Assert.DoesNotContain("Museum", original, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("front.jpg", original, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\\", original, StringComparison.Ordinal);
        Assert.DoesNotContain("C:", original, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", original, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("artifact-images/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/000003/", original, StringComparison.Ordinal);
    }
}

internal sealed record SeededInProgressUpload(
    Guid OperationId,
    Guid SetId,
    IReadOnlyList<Guid> ImageIds,
    IReadOnlyList<string> OriginalObjectKeys);

internal sealed class FaultingMuseumDbContext(MuseumDbContext inner) : IMuseumDbContext
{
    public Func<MuseumDbContext, PhotographyUploadOperation, Task>? OnOperationInsertFailureAsync { get; init; }
    public bool ThrowOperationInsertWithoutWinner { get; init; }
    public bool ThrowNextMarkSeenConcurrency { get; set; }
    public bool ThrowNextSuccessfulFileMetadataSave { get; set; }
    public int InsertFailuresThrown { get; private set; }
    public int MarkSeenConcurrencyFailuresThrown { get; private set; }
    public int SuccessfulFileMetadataFailuresThrown { get; private set; }
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

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var addedOperation = inner.ChangeTracker.Entries<PhotographyUploadOperation>()
            .FirstOrDefault(entry => entry.State == EntityState.Added)?.Entity;
        if (addedOperation is not null && (ThrowOperationInsertWithoutWinner || OnOperationInsertFailureAsync is not null))
        {
            InsertFailuresThrown++;
            if (OnOperationInsertFailureAsync is not null)
            {
                await OnOperationInsertFailureAsync(inner, addedOperation);
            }

            throw new DbUpdateException("Simulated competing idempotency insert.");
        }

        var modifiedOperation = inner.ChangeTracker.Entries<PhotographyUploadOperation>()
            .FirstOrDefault(entry => entry.State == EntityState.Modified)?.Entity;
        if (ThrowNextMarkSeenConcurrency && modifiedOperation is not null)
        {
            ThrowNextMarkSeenConcurrency = false;
            MarkSeenConcurrencyFailuresThrown++;
            throw new DbUpdateConcurrencyException("Simulated concurrent MarkSeen update.");
        }

        var hasAddedImage = inner.ChangeTracker.Entries<ArtifactImage>()
            .Any(entry => entry.State == EntityState.Added);
        var hasAddedSucceededOutcome = inner.ChangeTracker.Entries<PhotographyUploadFileOutcome>()
            .Any(entry => entry.State == EntityState.Added && entry.Entity.Status == PhotographyUploadFileOutcomeStatus.Succeeded);
        if (ThrowNextSuccessfulFileMetadataSave && hasAddedImage && hasAddedSucceededOutcome)
        {
            ThrowNextSuccessfulFileMetadataSave = false;
            SuccessfulFileMetadataFailuresThrown++;
            throw new DbUpdateException("Simulated successful file metadata persistence failure.");
        }

        return await inner.SaveChangesAsync(cancellationToken);
    }
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class ArtifactImageDeletionServiceTests
{
    private static readonly DateTimeOffset DeletionRequestedAt = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_image_returns_invalid_state_and_never_calls_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (service, storage) = NewService(db);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            Guid.NewGuid(), 0, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.InvalidState, result.Outcome);
        Assert.Empty(storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Non_available_image_returns_invalid_state_and_never_calls_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set, ArtifactImageStatus.DeletePending);
        await db.SaveChangesAsync();
        var (service, storage) = NewService(db);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.InvalidState, result.Outcome);
        Assert.Empty(storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Stale_expected_token_conflicts_and_never_calls_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var (service, storage) = NewService(db);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken + 1, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.Conflict, result.Outcome);
        Assert.Empty(storage.DeleteImageObjectCalls);
        var unchanged = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Available, unchanged.Status);
    }

    [Fact]
    public async Task Db_image_concurrency_exception_maps_to_conflict_without_calling_storage()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var faulting = new FaultingPhotographyManagementDbContext(db) { ThrowNextImageConcurrency = true };
        var (service, storage) = NewService(db, persistenceContext: faulting);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.Conflict, result.Outcome);
        Assert.Equal(1, faulting.ImageConcurrencyFailuresThrown);
        Assert.Empty(storage.DeleteImageObjectCalls);
    }

    [Fact]
    public async Task Primary_state_concurrency_conflict_prevents_storage_call_and_leaves_image_available()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = SeedAvailableImage(db);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var faulting = new FaultingPhotographyManagementDbContext(db) { ThrowNextStateConcurrency = true };
        var (service, storage) = NewService(db, persistenceContext: faulting);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.Conflict, result.Outcome);
        Assert.Equal(1, faulting.StateConcurrencyFailuresThrown);
        Assert.Empty(storage.DeleteImageObjectCalls);
        var unchangedImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Available, unchangedImage.Status);
        var unchangedState = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(image.ArtifactImageId, unchangedState.PrimaryImageId);
    }

    [Fact]
    public async Task Successful_grace_deletion_clears_current_primary_audits_the_change_and_finalizes_to_deleted()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = SeedAvailableImage(db, addDerivatives: true);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var (service, storage) = NewService(db);
        var originalObjectKey = image.OriginalObjectKey;
        var derivativeKeys = image.Derivatives.Select(derivative => derivative.ObjectKey).ToArray();

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.Completed, result.Outcome);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.Deleted, finalImage.Status);
        Assert.Equal("photographer-1", finalImage.DeletedByUserId);
        Assert.Equal(DeletionRequestedAt, finalImage.DeletedAt);
        var finalState = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Null(finalState.PrimaryImageId);
        var primaryAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.PrimaryImageChange);
        Assert.Contains($"PreviousPrimaryImageId={image.ArtifactImageId}", primaryAudit.ChangeSummary);
        Assert.Contains("NewPrimaryImageId=<null>", primaryAudit.ChangeSummary);
        var deletionAudit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeleteByUploaderGrace);
        Assert.Equal(image.ArtifactImageId.ToString(), deletionAudit.EntityId);
        Assert.Equal(deletionAudit.AuditEntryId.ToString(), result.AuditReference);
        var deleteCall = Assert.Single(storage.DeleteImageObjectCalls);
        Assert.Equal(originalObjectKey, deleteCall.Original);
        Assert.Equal(derivativeKeys, deleteCall.Derivatives);
    }

    [Fact]
    public async Task Deleting_non_primary_image_leaves_current_primary_unchanged_and_writes_no_primary_audit()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var primary = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var other = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(primary.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var (service, _) = NewService(db);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            other.ArtifactImageId, other.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.Completed, result.Outcome);
        var finalState = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(primary.ArtifactImageId, finalState.PrimaryImageId);
        var audits = await db.AuditEntries.ToListAsync();
        Assert.DoesNotContain(audits, entry => entry.ActionName == PhotographyAuditActions.PrimaryImageChange);
    }

    [Fact]
    public async Task Privileged_deletion_final_audit_retains_the_reason()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var (service, _) = NewService(db);

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.Privileged, "duplicate accession photo", "supervisor-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.Completed, result.Outcome);
        var finalImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal("duplicate accession photo", finalImage.DeletionReason);
        var audit = await db.AuditEntries.SingleAsync(entry => entry.ActionName == PhotographyAuditActions.ImageDeletePrivileged);
        Assert.Contains("Reason=duplicate accession photo", audit.ChangeSummary);
        Assert.DoesNotContain("ObjectKey", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Storage_partial_failure_leaves_image_delete_pending_records_recovery_and_reports_no_full_success()
    {
        await using var db = PhotographyUploadApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, addDerivatives: true);
        await db.SaveChangesAsync();
        var (service, storage) = NewService(db);
        var failedDerivativeKey = image.Derivatives.First().ObjectKey;
        storage.QueueDeleteResult(ArtifactImageObjectsDeleteResult.PartialFailure(
            [
                ArtifactImageStorageDeleteResult.Success(image.OriginalObjectKey),
                ArtifactImageStorageDeleteResult.Failed(failedDerivativeKey, ArtifactImageStorageResultKind.RetryableFailure, "Storage.RetryableFailure", "Image storage is currently unavailable.")
            ],
            "Storage.DeletePartialFailure",
            "One or more stored image objects could not be deleted.",
            "provider://internal/delete"));

        var result = await service.DeleteAsync(new AuthorizedArtifactImageDeletion(
            image.ArtifactImageId, image.ConcurrencyToken, ArtifactImageDeletionMode.UploaderGracePeriod, null, "photographer-1", DeletionRequestedAt));

        Assert.Equal(ArtifactImageDeletionOutcome.RecoveryRequired, result.Outcome);
        var pendingImage = await db.ArtifactImages.SingleAsync();
        Assert.Equal(ArtifactImageStatus.DeletePending, pendingImage.Status);
        var recovery = await db.StorageOperationRecoveries.SingleAsync();
        Assert.Equal(StorageOperationRecoveryType.DeleteCleanup, recovery.OperationType);
        Assert.Equal(image.ArtifactImageId, recovery.ArtifactImageId);
        Assert.Equal(1 + image.Derivatives.Count, recovery.ObjectKeys.Count);
        var audits = await db.AuditEntries.ToListAsync();
        Assert.DoesNotContain(audits, entry =>
            entry.ActionName is PhotographyAuditActions.ImageDeleteByUploaderGrace or PhotographyAuditActions.ImageDeletePrivileged);
    }

    [Fact]
    public void Deletion_result_and_authorized_request_do_not_expose_storage_internals_or_permission_data()
    {
        AssertNoForbiddenMembers(typeof(ArtifactImageDeletionResult), ["ObjectKey", "Bucket", "Endpoint", "Minio", "Presigned"]);
        AssertNoForbiddenMembers(typeof(AuthorizedArtifactImageDeletion), ["Permission", "HasManage", "HasUpload", "Role"]);
    }

    [Fact]
    public void Service_constructors_do_not_take_permission_checker_dependencies()
    {
        var deletionParameterTypeNames = typeof(ArtifactImageDeletionService)
            .GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();
        var finalizationParameterTypeNames = typeof(ArtifactImageDeletionFinalizationService)
            .GetConstructors().Single().GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(deletionParameterTypeNames, name => name.Contains("PermissionChecker", StringComparison.Ordinal));
        Assert.DoesNotContain(finalizationParameterTypeNames, name => name.Contains("PermissionChecker", StringComparison.Ordinal));
        Assert.DoesNotContain(finalizationParameterTypeNames, name => name.Contains("Storage", StringComparison.Ordinal));
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

    private static (ArtifactImageDeletionService Service, ScriptedArtifactImageStorage Storage) NewService(
        MuseumDbContext db,
        string actorUserId = "photographer-1",
        IMuseumDbContext? persistenceContext = null)
    {
        var context = persistenceContext ?? db;
        var actorContext = new TestAuditActorContext(actorUserId);
        var auditWriter = new AuditWriter(context, actorContext);
        var storage = new ScriptedArtifactImageStorage();
        var finalizationService = new ArtifactImageDeletionFinalizationService(context, auditWriter);
        var service = new ArtifactImageDeletionService(context, auditWriter, storage, finalizationService);
        return (service, storage);
    }

    private static (Artifact Artifact, PhotographySet Set, ArtifactImage Image) SeedAvailableImage(MuseumDbContext db, bool addDerivatives = false)
    {
        var artifact = PhotographyUploadApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);

        if (addDerivatives)
        {
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
        }

        return (artifact, set, image);
    }
}

internal sealed class ScriptedArtifactImageStorage : IArtifactImageStorage
{
    private readonly Queue<ArtifactImageObjectsDeleteResult> queuedDeleteResults = new();

    public List<(ImageStorageObjectKey Original, IReadOnlyCollection<ImageStorageObjectKey> Derivatives)> DeleteImageObjectCalls { get; } = [];

    public void QueueDeleteResult(ArtifactImageObjectsDeleteResult result) => queuedDeleteResults.Enqueue(result);

    public ValueTask<ArtifactImageObjectsDeleteResult> DeleteImageObjectsAsync(
        ImageStorageObjectKey originalObjectKey,
        IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys,
        CancellationToken cancellationToken = default)
    {
        DeleteImageObjectCalls.Add((originalObjectKey, derivativeObjectKeys));
        if (queuedDeleteResults.TryDequeue(out var queued))
        {
            return ValueTask.FromResult(queued);
        }

        var results = new[] { originalObjectKey }.Concat(derivativeObjectKeys)
            .Select(ArtifactImageStorageDeleteResult.Success)
            .ToArray();
        return ValueTask.FromResult(ArtifactImageObjectsDeleteResult.Success(results));
    }

    public ValueTask<ArtifactImageStorageWriteResult> StoreOriginalAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, string? checksum, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageWriteResult> StoreDerivativeAsync(ImageStorageObjectKey objectKey, Stream content, string contentType, long lengthBytes, ImageDerivativeKind derivativeKind, string? checksum, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageStatResult> StatAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageReadResult> OpenReadAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageShortLivedReadAccessResult> CreateShortLivedReadAccessAsync(ImageStorageObjectKey objectKey, TimeSpan requestedLifetime, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public ValueTask<ArtifactImageStorageDeleteResult> DeleteObjectAsync(ImageStorageObjectKey objectKey, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyUploadPersistenceService(IMuseumDbContext dbContext)
{
    public Task<bool> ArtifactExistsAsync(Guid artifactId, CancellationToken cancellationToken = default) =>
        dbContext.Artifacts.AnyAsync(artifact => artifact.ArtifactId == artifactId, cancellationToken);

    public Task<PhotographySet?> FindPhotographySetAsync(Guid photographySetId, CancellationToken cancellationToken = default) =>
        dbContext.PhotographySets.FirstOrDefaultAsync(set => set.PhotographySetId == photographySetId, cancellationToken);

    public Task<PhotographyUploadOperation?> FindUploadOperationAsync(
        string actorUserId,
        PhotographyUploadOperationKind operationKind,
        string idempotencyKey,
        CancellationToken cancellationToken = default) =>
        dbContext.PhotographyUploadOperations
            .Include(operation => operation.FileOutcomes)
            .FirstOrDefaultAsync(operation =>
                operation.ActorUserId == actorUserId
                && operation.OperationKind == operationKind
                && operation.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<PhotographyUploadOperation> LoadUploadOperationAsync(Guid operationId, CancellationToken cancellationToken = default) =>
        dbContext.PhotographyUploadOperations
            .Include(operation => operation.FileOutcomes)
            .FirstAsync(operation => operation.PhotographyUploadOperationId == operationId, cancellationToken);

    public async Task<PhotographyUploadOperation> GetOrStartUploadOperationAsync(
        string actorUserId,
        PhotographyUploadOperationKind operationKind,
        string idempotencyKey,
        string requestFingerprint,
        Guid artifactId,
        Guid? photographySetId = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindUploadOperationAsync(actorUserId, operationKind, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var operation = PhotographyUploadOperation.Start(actorUserId, operationKind, idempotencyKey, requestFingerprint, artifactId, photographySetId);
        dbContext.PhotographyUploadOperations.Add(operation);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return operation;
        }
        catch (DbUpdateException) when (OperationInsertMayHaveLostRace(operation))
        {
            dbContext.ClearTrackedChanges();
            var raced = await FindUploadOperationAsync(actorUserId, operationKind, idempotencyKey, cancellationToken);
            if (raced is not null)
            {
                return raced;
            }

            throw;
        }
    }

    public async Task<PhotographyUploadOperation> MarkOperationSeenAndReloadAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await LoadUploadOperationAsync(operationId, cancellationToken);
        operation.MarkSeen();
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ClearTrackedChanges();
        }

        return await LoadUploadOperationAsync(operationId, cancellationToken);
    }

    public async Task<PhotographyUploadFileOutcome> PersistRejectedOutcomeAsync(
        Guid operationId,
        int clientFileOrdinal,
        string originalFilename,
        string fileFingerprint,
        string staffFacingMessage,
        CancellationToken cancellationToken = default)
    {
        var outcome = PhotographyUploadFileOutcome.Rejected(
            operationId,
            clientFileOrdinal,
            originalFilename,
            fileFingerprint,
            staffFacingMessage);

        await PersistOutcomeAsync(operationId, outcome, cancellationToken);
        return outcome;
    }

    public async Task<PhotographyUploadFileOutcome> PersistFailedOutcomeAsync(
        Guid operationId,
        int clientFileOrdinal,
        string originalFilename,
        string fileFingerprint,
        string staffFacingMessage,
        CancellationToken cancellationToken = default)
    {
        var outcome = PhotographyUploadFileOutcome.Failed(
            operationId,
            clientFileOrdinal,
            originalFilename,
            fileFingerprint,
            staffFacingMessage);

        await PersistOutcomeAsync(operationId, outcome, cancellationToken);
        return outcome;
    }

    public async Task<PhotographyUploadFileOutcome> PersistRecoveryNeededOutcomeAsync(
        Guid operationId,
        int clientFileOrdinal,
        string originalFilename,
        string fileFingerprint,
        string staffFacingMessage,
        ImageStorageObjectKey? originalObjectKey,
        IReadOnlyCollection<ImageStorageObjectKey> derivativeObjectKeys,
        IReadOnlyCollection<ImageStorageObjectKey> recoveryObjectKeys,
        Guid artifactId,
        string failureSummary,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var operation = await LoadUploadOperationAsync(operationId, cancellationToken);
            var outcome = PhotographyUploadFileOutcome.RecoveryNeeded(
                operation.PhotographyUploadOperationId,
                clientFileOrdinal,
                originalFilename,
                fileFingerprint,
                staffFacingMessage,
                originalObjectKey,
                derivativeObjectKeys);
            var recovery = StorageOperationRecovery.Create(
                StorageOperationRecoveryType.UploadCleanup,
                artifactId,
                recoveryObjectKeys,
                failureSummary,
                artifactImageId: null,
                photographyUploadOperationId: operation.PhotographyUploadOperationId,
                photographyUploadFileOutcomeId: outcome.PhotographyUploadFileOutcomeId);

            dbContext.StorageOperationRecoveries.Add(recovery);
            operation.AddFileOutcome(outcome);
            dbContext.PhotographyUploadFileOutcomes.Add(outcome);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return outcome;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }
    }

    public async Task<PhotographySet> PersistSuccessfulFileAsync(
        Guid operationId,
        PhotographySet? existingSet,
        PhotographySet? newSet,
        ArtifactImage image,
        IReadOnlyCollection<ArtifactImageDerivative> derivatives,
        PhotographyUploadFileOutcome outcome,
        CancellationToken cancellationToken = default)
    {
        var set = existingSet ?? newSet ?? throw new ArgumentException("A photography set is required for successful file persistence.", nameof(newSet));
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            var operation = await LoadUploadOperationAsync(operationId, cancellationToken);
            if (newSet is not null)
            {
                dbContext.PhotographySets.Add(newSet);
            }

            dbContext.ArtifactImages.Add(image);
            dbContext.ArtifactImageDerivatives.AddRange(derivatives);
            operation.AddFileOutcome(outcome);
            dbContext.PhotographyUploadFileOutcomes.Add(outcome);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (operation.OperationKind == PhotographyUploadOperationKind.CreateSetUpload && operation.PhotographySetId is null)
            {
                operation.AttachPhotographySet(set.PhotographySetId);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return set;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }
    }

    public async Task FinalizeOperationAsync(Guid operationId, int expectedFileCount, CancellationToken cancellationToken = default)
    {
        var operation = await LoadUploadOperationAsync(operationId, cancellationToken);
        if (operation.Status != PhotographyUploadOperationStatus.InProgress)
        {
            return;
        }

        operation.FinalizeBatch(expectedFileCount);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PhotographyUploadOperationSnapshot> LoadAuthoritativeSnapshotAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await dbContext.PhotographyUploadOperations
            .Include(uploadOperation => uploadOperation.FileOutcomes)
            .FirstAsync(uploadOperation => uploadOperation.PhotographyUploadOperationId == operationId, cancellationToken);

        PhotographySet? set = null;
        if (operation.PhotographySetId.HasValue)
        {
            set = await dbContext.PhotographySets.FirstOrDefaultAsync(
                photographySet => photographySet.PhotographySetId == operation.PhotographySetId.Value,
                cancellationToken);
        }

        var imageIds = operation.FileOutcomes
            .Where(outcome => outcome.ArtifactImageId.HasValue)
            .Select(outcome => outcome.ArtifactImageId!.Value)
            .Distinct()
            .ToArray();

        var images = imageIds.Length == 0
            ? []
            : await dbContext.ArtifactImages
                .Include(image => image.Derivatives)
                .Where(image => imageIds.Contains(image.ArtifactImageId))
                .ToListAsync(cancellationToken);

        return new PhotographyUploadOperationSnapshot(operation, set, images);
    }

    private async Task PersistOutcomeAsync(Guid operationId, PhotographyUploadFileOutcome outcome, CancellationToken cancellationToken)
    {
        var operation = await LoadUploadOperationAsync(operationId, cancellationToken);
        operation.AddFileOutcome(outcome);
        dbContext.PhotographyUploadFileOutcomes.Add(outcome);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool OperationInsertMayHaveLostRace(PhotographyUploadOperation operation) =>
        operation.PhotographyUploadOperationId != Guid.Empty;
}

public sealed record PhotographyUploadOperationSnapshot(
    PhotographyUploadOperation Operation,
    PhotographySet? PhotographySet,
    IReadOnlyList<ArtifactImage> Images);

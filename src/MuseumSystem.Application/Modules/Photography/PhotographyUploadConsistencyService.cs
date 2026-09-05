using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyUploadConsistencyService(
    PhotographyUploadPersistenceService persistence,
    IArtifactImageStorage storage,
    PhotographyObjectKeyFactory objectKeyFactory,
    ArtifactImageStorageHealthService storageHealth,
    PhotographyUploadAuditService auditService)
{
    internal async Task<PhotographySet?> ProcessFileAsync(
        PhotographyUploadOperation operation,
        PreparedPhotographyUploadFile file,
        Guid artifactId,
        PhotographyPurpose purpose,
        DateOnly photographyDate,
        string photographerUserId,
        string actorUserId,
        PhotographySet? existingSet,
        bool createSetWhenSuccessful,
        CancellationToken cancellationToken)
    {
        if (file.Validation.Rejection is not null)
        {
            var outcome = await persistence.PersistRejectedOutcomeAsync(
                operation.PhotographyUploadOperationId,
                file.Input.ClientFileOrdinal,
                file.Input.OriginalFilename.Trim(),
                file.FileFingerprint,
                file.Validation.Rejection.StaffFacingMessage,
                cancellationToken);
            operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId, cancellationToken);
            await auditService.WriteFileOutcomeAsync(operation, outcome, cancellationToken);
            return existingSet;
        }

        if (file.Validation.Failure is not null)
        {
            var outcome = await persistence.PersistFailedOutcomeAsync(
                operation.PhotographyUploadOperationId,
                file.Input.ClientFileOrdinal,
                file.Input.OriginalFilename.Trim(),
                file.FileFingerprint,
                file.Validation.Failure.StaffFacingMessage,
                cancellationToken);
            operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId, cancellationToken);
            await auditService.WriteFileOutcomeAsync(operation, outcome, cancellationToken);
            return existingSet;
        }

        var media = file.Validation.Media!;
        await file.ResetAsync(cancellationToken);
        var derivativeGeneration = await file.Processor.GenerateDerivativesAsync(file.Content, media, cancellationToken);
        await file.ResetAsync(cancellationToken);
        if (!derivativeGeneration.Succeeded)
        {
            var outcome = await persistence.PersistFailedOutcomeAsync(
                operation.PhotographyUploadOperationId,
                file.Input.ClientFileOrdinal,
                file.Input.OriginalFilename.Trim(),
                file.FileFingerprint,
                derivativeGeneration.Failure?.StaffFacingMessage ?? "Image derivatives could not be generated.",
                cancellationToken);
            operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId, cancellationToken);
            await auditService.WriteFileOutcomeAsync(operation, outcome, cancellationToken);
            return existingSet;
        }

        try
        {
            return await StoreAndPersistSuccessfulFileAsync(
                operation,
                file,
                derivativeGeneration.Derivatives,
                artifactId,
                purpose,
                photographyDate,
                photographerUserId,
                actorUserId,
                existingSet,
                createSetWhenSuccessful,
                media,
                cancellationToken);
        }
        finally
        {
            foreach (var derivative in derivativeGeneration.Derivatives)
            {
                await derivative.Content.DisposeAsync();
            }
        }
    }

    private async Task<PhotographySet?> StoreAndPersistSuccessfulFileAsync(
        PhotographyUploadOperation operation,
        PreparedPhotographyUploadFile file,
        IReadOnlyList<ArtifactImageDerivativeContent> derivativeContents,
        Guid artifactId,
        PhotographyPurpose purpose,
        DateOnly photographyDate,
        string photographerUserId,
        string actorUserId,
        PhotographySet? existingSet,
        bool createSetWhenSuccessful,
        ArtifactImageMediaDescriptor media,
        CancellationToken cancellationToken)
    {
        var keyInput = new PhotographyObjectKeyInput(
            operation.PhotographyUploadOperationId,
            file.Input.ClientFileOrdinal,
            file.FileFingerprint,
            media.NormalizedExtension);
        var originalKey = objectKeyFactory.CreateOriginalKey(keyInput);
        var derivativeKeys = derivativeContents
            .Select(derivative => objectKeyFactory.CreateDerivativeKey(keyInput, derivative.Kind, derivative.NormalizedExtension))
            .ToArray();
        var storedDerivativeKeys = new List<ImageStorageObjectKey>();

        await file.ResetAsync(cancellationToken);
        var originalWrite = await storage.StoreOriginalAsync(originalKey, file.Content, media.ContentType, media.LengthBytes, file.ContentHash, cancellationToken);
        await file.ResetAsync(cancellationToken);
        var originalResolution = await ResolveStoredObjectAsync(
            originalWrite,
            new IntendedStoredObject(originalKey, media.ContentType, media.LengthBytes, file.ContentHash),
            "Original image could not be stored.",
            cancellationToken);
        if (originalResolution.Status == StoredObjectResolutionStatus.DefinitelyAbsent)
        {
            var outcome = await persistence.PersistFailedOutcomeAsync(
                operation.PhotographyUploadOperationId,
                file.Input.ClientFileOrdinal,
                file.Input.OriginalFilename.Trim(),
                file.FileFingerprint,
                originalResolution.StaffFacingMessage,
                cancellationToken);
            operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId, cancellationToken);
            await auditService.WriteFileOutcomeAsync(operation, outcome, cancellationToken);
            return existingSet;
        }

        if (originalResolution.Status != StoredObjectResolutionStatus.Established)
        {
            return await RecordFailedAfterStorageAsync(
                operation.PhotographyUploadOperationId,
                file,
                artifactId,
                [originalKey],
                originalResolution.StaffFacingMessage,
                originalResolution.OperationalSummary,
                existingSet,
                cancellationToken);
        }

        for (var index = 0; index < derivativeContents.Count; index++)
        {
            var derivative = derivativeContents[index];
            var derivativeWrite = await storage.StoreDerivativeAsync(
                derivativeKeys[index],
                derivative.Content,
                derivative.ContentType,
                derivative.LengthBytes,
                derivative.Kind,
                null,
                cancellationToken);

            var attemptedDerivativeKey = derivativeKeys[index];
            var derivativeResolution = await ResolveStoredObjectAsync(
                derivativeWrite,
                new IntendedStoredObject(attemptedDerivativeKey, derivative.ContentType, derivative.LengthBytes, null),
                "Image derivative could not be stored.",
                cancellationToken);

            if (derivativeResolution.Status == StoredObjectResolutionStatus.DefinitelyAbsent)
            {
                return await RecordFailedAfterStorageAsync(
                    operation.PhotographyUploadOperationId,
                    file,
                    artifactId,
                    [originalKey, .. storedDerivativeKeys],
                    derivativeResolution.StaffFacingMessage,
                    derivativeResolution.OperationalSummary,
                    existingSet,
                    cancellationToken);
            }

            if (derivativeResolution.Status != StoredObjectResolutionStatus.Established)
            {
                return await RecordFailedAfterStorageAsync(
                    operation.PhotographyUploadOperationId,
                    file,
                    artifactId,
                    [originalKey, .. storedDerivativeKeys, attemptedDerivativeKey],
                    derivativeResolution.StaffFacingMessage,
                    derivativeResolution.OperationalSummary,
                    existingSet,
                    cancellationToken);
            }

            storedDerivativeKeys.Add(attemptedDerivativeKey);
        }

        var verification = await VerifyStoredObjectsAsync(
            originalKey,
            media,
            file.ContentHash,
            derivativeKeys,
            derivativeContents,
            cancellationToken);
        if (verification is not null)
        {
            return await RecordFailedAfterStorageAsync(
                operation.PhotographyUploadOperationId,
                file,
                artifactId,
                [originalKey, .. storedDerivativeKeys],
                verification.StaffFacingMessage,
                verification.OperationalSummary,
                existingSet,
                cancellationToken);
        }

        var setToCreate = createSetWhenSuccessful ? PhotographySet.Create(artifactId, purpose, photographyDate, photographerUserId, actorUserId) : null;
        var targetSet = existingSet ?? setToCreate!;
        var image = ArtifactImage.Create(
            artifactId,
            targetSet.PhotographySetId,
            originalKey,
            file.Input.OriginalFilename.Trim(),
            media.ContentType,
            media.LengthBytes,
            media.PixelWidth,
            media.PixelHeight,
            actorUserId,
            DateTimeOffset.UtcNow);
        var derivatives = derivativeContents.Select((derivative, index) => ArtifactImageDerivative.Create(
            image.ArtifactImageId,
            derivative.Kind,
            derivativeKeys[index],
            derivative.ContentType,
            derivative.LengthBytes,
            derivative.PixelWidth,
            derivative.PixelHeight)).ToArray();

        foreach (var derivative in derivatives)
        {
            image.AddDerivative(derivative);
        }

        var successOutcome = PhotographyUploadFileOutcome.Succeeded(
            operation.PhotographyUploadOperationId,
            file.Input.ClientFileOrdinal,
            file.Input.OriginalFilename.Trim(),
            file.FileFingerprint,
            image.ArtifactImageId,
            originalKey,
            derivativeKeys,
            "File uploaded.");

        try
        {
            var persistedSet = await persistence.PersistSuccessfulFileAsync(
                operation.PhotographyUploadOperationId,
                existingSet,
                setToCreate,
                image,
                derivatives,
                successOutcome,
                cancellationToken);
            await auditService.WriteFileOutcomeAsync(operation, successOutcome, cancellationToken);
            return persistedSet;
        }
        catch (DbUpdateException ex)
        {
            return await RecordFailedAfterStorageAsync(
                operation.PhotographyUploadOperationId,
                file,
                artifactId,
                [originalKey, .. derivativeKeys],
                "Image metadata could not be saved after storage.",
                ex.Message,
                existingSet,
                cancellationToken);
        }
    }

    private async Task<StoredObjectVerificationFailure?> VerifyStoredObjectsAsync(
        ImageStorageObjectKey originalKey,
        ArtifactImageMediaDescriptor media,
        string contentHash,
        IReadOnlyList<ImageStorageObjectKey> derivativeKeys,
        IReadOnlyList<ArtifactImageDerivativeContent> derivativeContents,
        CancellationToken cancellationToken)
    {
        var originalStat = await storage.StatAsync(originalKey, cancellationToken);
        if (!IsCompatible(originalStat.StoredObject, new IntendedStoredObject(originalKey, media.ContentType, media.LengthBytes, contentHash)))
        {
            var assessment = storageHealth.Assess(originalStat.Kind);
            return new StoredObjectVerificationFailure(
                originalStat.Kind == ArtifactImageStorageResultKind.Success
                    ? "Stored original image could not be verified."
                    : assessment.CanonicalStaffFacingMessage,
                originalStat.Kind == ArtifactImageStorageResultKind.Success
                    ? "Stored object metadata did not match the expected upload."
                    : assessment.OperationalSummary);
        }

        for (var index = 0; index < derivativeKeys.Count; index++)
        {
            var derivative = derivativeContents[index];
            var derivativeStat = await storage.StatAsync(derivativeKeys[index], cancellationToken);
            if (!IsCompatible(derivativeStat.StoredObject, new IntendedStoredObject(derivativeKeys[index], derivative.ContentType, derivative.LengthBytes, null)))
            {
                var assessment = storageHealth.Assess(derivativeStat.Kind);
                return new StoredObjectVerificationFailure(
                    derivativeStat.Kind == ArtifactImageStorageResultKind.Success
                        ? "Stored image derivative could not be verified."
                        : assessment.CanonicalStaffFacingMessage,
                    derivativeStat.Kind == ArtifactImageStorageResultKind.Success
                        ? "Stored object metadata did not match the expected upload."
                        : assessment.OperationalSummary);
            }
        }

        return null;
    }

    private async Task<PhotographySet?> RecordFailedAfterStorageAsync(
        Guid operationId,
        PreparedPhotographyUploadFile file,
        Guid artifactId,
        IReadOnlyCollection<ImageStorageObjectKey> objectKeys,
        string staffFacingFailure,
        string? operationalSummary,
        PhotographySet? existingSet,
        CancellationToken cancellationToken)
    {
        var cleanup = await storage.DeleteImageObjectsAsync(objectKeys.First(), objectKeys.Skip(1).ToArray(), cancellationToken);
        var cleanupAssessment = storageHealth.Assess(cleanup.Kind);
        var operation = await persistence.LoadUploadOperationAsync(operationId, cancellationToken);
        PhotographyUploadFileOutcome outcome;
        if (cleanup.Succeeded)
        {
            outcome = await persistence.PersistFailedOutcomeAsync(
                operation.PhotographyUploadOperationId,
                file.Input.ClientFileOrdinal,
                file.Input.OriginalFilename.Trim(),
                file.FileFingerprint,
                staffFacingFailure,
                cancellationToken);
        }
        else
        {
            var unresolvedObjectKeys = cleanup.ObjectResults
                .Where(result => !result.Succeeded)
                .Select(result => result.ObjectKey)
                .Distinct()
                .ToArray();
            if (unresolvedObjectKeys.Length == 0)
            {
                outcome = await persistence.PersistFailedOutcomeAsync(
                    operation.PhotographyUploadOperationId,
                    file.Input.ClientFileOrdinal,
                    file.Input.OriginalFilename.Trim(),
                    file.FileFingerprint,
                    staffFacingFailure,
                    cancellationToken);
            }
            else
            {
                var originalObjectKey = objectKeys.First();
                var unresolvedSet = unresolvedObjectKeys.ToHashSet();
                outcome = await persistence.PersistRecoveryNeededOutcomeAsync(
                    operation.PhotographyUploadOperationId,
                    file.Input.ClientFileOrdinal,
                    file.Input.OriginalFilename.Trim(),
                    file.FileFingerprint,
                    "Storage cleanup could not be completed. Recovery is required.",
                    unresolvedSet.Contains(originalObjectKey) ? originalObjectKey : null,
                    objectKeys.Skip(1).Where(unresolvedSet.Contains).ToArray(),
                    unresolvedObjectKeys,
                    artifactId,
                    operationalSummary ?? cleanupAssessment.OperationalSummary ?? "Object storage cleanup did not complete safely.",
                    cancellationToken);
            }
        }

        operation = await persistence.LoadUploadOperationAsync(operation.PhotographyUploadOperationId, cancellationToken);
        await auditService.WriteFileOutcomeAsync(operation, outcome, cancellationToken);
        return existingSet;
    }

    private async Task<StoredObjectResolution> ResolveStoredObjectAsync(
        ArtifactImageStorageWriteResult writeResult,
        IntendedStoredObject intended,
        string fallbackStaffFacingMessage,
        CancellationToken cancellationToken)
    {
        if (writeResult.Succeeded)
        {
            return StoredObjectResolution.Established();
        }

        var writeAssessment = storageHealth.Assess(writeResult.Kind, ArtifactImageStorageOperationContext.Write);
        if (writeAssessment.IsMissing || !writeAssessment.RequiresAuthoritativeWriteVerification)
        {
            return StoredObjectResolution.DefinitelyAbsent(writeAssessment.CanonicalStaffFacingMessage, writeAssessment.OperationalSummary);
        }

        var stat = await storage.StatAsync(intended.ObjectKey, cancellationToken);
        if (stat.Exists)
        {
            return IsCompatible(stat.StoredObject, intended)
                ? StoredObjectResolution.Established()
                : StoredObjectResolution.Incompatible("Stored object did not match the expected upload.", writeAssessment.OperationalSummary);
        }

        var statAssessment = storageHealth.Assess(stat.Kind);
        if (statAssessment.IsMissing)
        {
            return StoredObjectResolution.DefinitelyAbsent(writeAssessment.CanonicalStaffFacingMessage, writeAssessment.OperationalSummary);
        }

        return StoredObjectResolution.Unknown(
            "Storage state could not be verified. Recovery is required.",
            statAssessment.OperationalSummary ?? writeAssessment.OperationalSummary);
    }

    private static bool IsCompatible(ArtifactImageStoredObjectMetadata? stored, IntendedStoredObject intended)
    {
        if (stored is null)
        {
            return false;
        }

        if (stored.ObjectKey != intended.ObjectKey || stored.LengthBytes != intended.LengthBytes)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(stored.ContentType)
            && !string.Equals(stored.ContentType, intended.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(stored.Checksum)
            || string.IsNullOrWhiteSpace(intended.Checksum)
            || string.Equals(stored.Checksum, intended.Checksum, StringComparison.OrdinalIgnoreCase);
    }
}

internal enum StoredObjectResolutionStatus
{
    Established,
    DefinitelyAbsent,
    Incompatible,
    Unknown
}

internal sealed record IntendedStoredObject(
    ImageStorageObjectKey ObjectKey,
    string ContentType,
    long LengthBytes,
    string? Checksum);

internal sealed record StoredObjectResolution(
    StoredObjectResolutionStatus Status,
    string StaffFacingMessage,
    string? OperationalSummary)
{
    public static StoredObjectResolution Established() =>
        new(StoredObjectResolutionStatus.Established, string.Empty, null);

    public static StoredObjectResolution DefinitelyAbsent(string staffFacingMessage, string? operationalSummary) =>
        new(StoredObjectResolutionStatus.DefinitelyAbsent, staffFacingMessage, operationalSummary);

    public static StoredObjectResolution Incompatible(string staffFacingMessage, string? operationalSummary) =>
        new(StoredObjectResolutionStatus.Incompatible, staffFacingMessage, operationalSummary);

    public static StoredObjectResolution Unknown(string staffFacingMessage, string? operationalSummary) =>
        new(StoredObjectResolutionStatus.Unknown, staffFacingMessage, operationalSummary);
}

internal sealed record StoredObjectVerificationFailure(
    string StaffFacingMessage,
    string? OperationalSummary);

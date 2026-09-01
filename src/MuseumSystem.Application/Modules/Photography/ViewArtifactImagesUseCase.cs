using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class ViewArtifactImagesUseCase(
    IMuseumDbContext dbContext,
    ICurrentActorPermissionChecker permissionChecker,
    IArtifactImageStorage storage,
    PhotographyGalleryMapper mapper)
{
    public async Task<UseCaseResult<ArtifactImageGalleryDto>> ViewArtifactImages(
        ViewArtifactImagesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var permissionFailure = RequireViewPermission();
        if (permissionFailure is not null)
        {
            return UseCaseResult<ArtifactImageGalleryDto>.Failure(permissionFailure);
        }

        if (query.ArtifactId == Guid.Empty)
        {
            return UseCaseResult<ArtifactImageGalleryDto>.Failure(new ValidationIssue("Artifact.Required", "Artifact is required.", nameof(query.ArtifactId)));
        }

        var artifact = await dbContext.Artifacts
            .Include(item => item.Category)
            .Include(item => item.CurrentLocation)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ArtifactId == query.ArtifactId, cancellationToken);
        if (artifact is null)
        {
            return UseCaseResult<ArtifactImageGalleryDto>.Failure(new ValidationIssue("Artifact.NotFound", "Artifact was not found.", nameof(query.ArtifactId)));
        }

        var images = await dbContext.ArtifactImages
            .Include(image => image.Derivatives)
            .AsNoTracking()
            .Where(image => image.ArtifactId == query.ArtifactId && image.Status == ArtifactImageStatus.Available)
            .OrderByDescending(image => image.UploadedAt)
            .ThenBy(image => image.ArtifactImageId)
            .ToListAsync(cancellationToken);
        var sets = await LoadSetsAsync(images.Select(image => image.PhotographySetId).Distinct().ToArray(), cancellationToken);

        var galleryImages = new List<PhotographyGalleryImageDto>(images.Count);
        foreach (var image in images)
        {
            if (!sets.TryGetValue(image.PhotographySetId, out var set))
            {
                continue;
            }

            var thumbnail = await ProbeRenditionAsync(image, PhotographyImageRendition.Thumbnail, cancellationToken);
            var preview = await ProbeRenditionAsync(image, PhotographyImageRendition.Preview, cancellationToken);
            galleryImages.Add(mapper.ToImage(image, set, thumbnail, preview));
        }

        return UseCaseResult<ArtifactImageGalleryDto>.Success(mapper.ToGallery(artifact, galleryImages));
    }

    public async Task<UseCaseResult<ArtifactImageSafeReadResult>> ReadArtifactImageRendition(
        ReadArtifactImageRenditionQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var permissionFailure = RequireViewPermission();
        if (permissionFailure is not null)
        {
            return UseCaseResult<ArtifactImageSafeReadResult>.Failure(permissionFailure);
        }

        if (query.ArtifactImageId == Guid.Empty)
        {
            return UseCaseResult<ArtifactImageSafeReadResult>.Failure(new ValidationIssue("ArtifactImage.Required", "Artifact image is required.", nameof(query.ArtifactImageId)));
        }

        if (!IsSupportedRendition(query.Rendition))
        {
            return UseCaseResult<ArtifactImageSafeReadResult>.Failure(new ValidationIssue("ArtifactImage.RenditionUnsupported", "Requested image rendition is not supported.", nameof(query.Rendition)));
        }

        var image = await dbContext.ArtifactImages
            .Include(candidate => candidate.Derivatives)
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ArtifactImageId == query.ArtifactImageId, cancellationToken);
        if (image is null || image.Status != ArtifactImageStatus.Available)
        {
            return UseCaseResult<ArtifactImageSafeReadResult>.Success(ArtifactImageSafeReadResult.NotFound());
        }

        var derivative = FindDerivative(image, query.Rendition);
        if (derivative is null)
        {
            return UseCaseResult<ArtifactImageSafeReadResult>.Success(ArtifactImageSafeReadResult.NotFound());
        }

        var read = await storage.OpenReadAsync(derivative.ObjectKey, cancellationToken);
        if (!read.Succeeded || read.ReadStream is null)
        {
            if (read.Kind == ArtifactImageStorageResultKind.NotFound)
            {
                await RecordMissingObjectRecoveryAsync(
                    image.ArtifactId,
                    image.ArtifactImageId,
                    [derivative.ObjectKey],
                    "Stored image rendition is missing.",
                    cancellationToken);
            }

            return UseCaseResult<ArtifactImageSafeReadResult>.Success(ArtifactImageSafeReadResult.Unavailable());
        }

        return UseCaseResult<ArtifactImageSafeReadResult>.Success(ArtifactImageSafeReadResult.Available(
            read.ReadStream.Content,
            read.ReadStream.Metadata.ContentType,
            read.ReadStream.Metadata.LengthBytes,
            SafeFilename(image.OriginalFilename, query.Rendition, derivative.ContentType)));
    }

    private ValidationIssue? RequireViewPermission() =>
        permissionChecker.HasPermission(PermissionNames.PhotographyView)
            ? null
            : PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyView);

    private async Task<IReadOnlyDictionary<Guid, PhotographySet>> LoadSetsAsync(
        IReadOnlyCollection<Guid> photographySetIds,
        CancellationToken cancellationToken)
    {
        if (photographySetIds.Count == 0)
        {
            return new Dictionary<Guid, PhotographySet>();
        }

        return await dbContext.PhotographySets
            .AsNoTracking()
            .Where(set => photographySetIds.Contains(set.PhotographySetId))
            .ToDictionaryAsync(set => set.PhotographySetId, cancellationToken);
    }

    private async Task<PhotographyGalleryRenditionDto> ProbeRenditionAsync(
        ArtifactImage image,
        PhotographyImageRendition rendition,
        CancellationToken cancellationToken)
    {
        var derivative = FindDerivative(image, rendition);
        if (derivative is null)
        {
            return mapper.ToUnavailableRendition(rendition, null);
        }

        var stat = await storage.StatAsync(derivative.ObjectKey, cancellationToken);
        if (stat.Exists && stat.StoredObject is not null)
        {
            return mapper.ToAvailableRendition(
                rendition,
                image.ArtifactImageId,
                stat.StoredObject.ContentType,
                stat.StoredObject.LengthBytes,
                derivative.PixelWidth,
                derivative.PixelHeight);
        }

        if (stat.Kind == ArtifactImageStorageResultKind.NotFound)
        {
            await RecordMissingObjectRecoveryAsync(
                image.ArtifactId,
                image.ArtifactImageId,
                [derivative.ObjectKey],
                "Stored image rendition is missing.",
                cancellationToken);
        }

        return mapper.ToUnavailableRendition(rendition, derivative);
    }

    private async Task RecordMissingObjectRecoveryAsync(
        Guid artifactId,
        Guid artifactImageId,
        IReadOnlyCollection<ImageStorageObjectKey> objectKeys,
        string failureSummary,
        CancellationToken cancellationToken)
    {
        var openRecoveries = await dbContext.StorageOperationRecoveries
            .AsNoTracking()
            .Where(recovery =>
                recovery.OperationType == StorageOperationRecoveryType.MissingObject
                && recovery.ArtifactId == artifactId
                && recovery.ArtifactImageId == artifactImageId
                && recovery.Status != StorageOperationRecoveryStatus.Resolved)
            .ToListAsync(cancellationToken);
        if (openRecoveries.Any(recovery => recovery.ObjectKeys.Any(existing => objectKeys.Contains(existing))))
        {
            return;
        }

        dbContext.StorageOperationRecoveries.Add(StorageOperationRecovery.Create(
            StorageOperationRecoveryType.MissingObject,
            artifactId,
            objectKeys,
            failureSummary,
            artifactImageId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ArtifactImageDerivative? FindDerivative(ArtifactImage image, PhotographyImageRendition rendition) =>
        image.Derivatives
            .OrderBy(derivative => derivative.CreatedAt)
            .FirstOrDefault(derivative => derivative.Kind == ToDerivativeKind(rendition));

    private static ImageDerivativeKind ToDerivativeKind(PhotographyImageRendition rendition) =>
        rendition switch
        {
            PhotographyImageRendition.Thumbnail => ImageDerivativeKind.Thumbnail,
            PhotographyImageRendition.Preview => ImageDerivativeKind.Preview,
            _ => throw new ArgumentOutOfRangeException(nameof(rendition), rendition, "Unsupported image rendition.")
        };

    private static bool IsSupportedRendition(PhotographyImageRendition rendition) =>
        rendition is PhotographyImageRendition.Thumbnail or PhotographyImageRendition.Preview;

    private static string SafeFilename(string originalFilename, PhotographyImageRendition rendition, string contentType)
    {
        var baseName = Path.GetFileNameWithoutExtension(originalFilename);
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseName.Where(character => !invalidCharacters.Contains(character)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "artifact-image";
        }

        var extension = contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        return $"{sanitized}-{rendition.ToString().ToLowerInvariant()}{extension}";
    }
}

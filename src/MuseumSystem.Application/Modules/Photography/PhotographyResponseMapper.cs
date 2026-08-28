using MuseumSystem.Application.Modules.Photography.Contracts;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class PhotographyResponseMapper
{
    public PhotographyUploadOperationResultDto ToUploadOperationResult(PhotographyUploadOperationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var imagesById = snapshot.Images.ToDictionary(image => image.ArtifactImageId);
        var set = snapshot.PhotographySet is null ? null : ToSetSummary(snapshot.PhotographySet, snapshot.Images.Count(image => image.PhotographySetId == snapshot.PhotographySet.PhotographySetId));
        var fileResults = snapshot.Operation.FileOutcomes
            .OrderBy(outcome => outcome.ClientFileOrdinal)
            .Select(outcome => ToFileResult(outcome, imagesById))
            .ToArray();

        return new PhotographyUploadOperationResultDto(
            snapshot.Operation.PhotographyUploadOperationId,
            snapshot.Operation.OperationKind,
            snapshot.Operation.Status,
            snapshot.Operation.ArtifactId,
            snapshot.Operation.PhotographySetId,
            set,
            fileResults,
            snapshot.Operation.StartedAt,
            snapshot.Operation.CompletedAt);
    }

    public PhotographySetSummaryDto ToSetSummary(PhotographySet set, int imageCount) =>
        new(
            set.PhotographySetId,
            set.ArtifactId,
            set.Purpose,
            set.PhotographyDate,
            set.PhotographerUserId,
            set.CreatedAt,
            set.CreatedByUserId,
            imageCount,
            set.ConcurrencyToken);

    public ArtifactImageSummaryDto ToImageSummary(ArtifactImage image) =>
        new(
            image.ArtifactImageId,
            image.ArtifactId,
            image.PhotographySetId,
            image.OriginalFilename,
            image.ContentType,
            image.FileSizeBytes,
            image.PixelWidth,
            image.PixelHeight,
            image.UploadedByUserId,
            image.UploadedAt,
            image.Caption,
            image.Status,
            image.Derivatives.OrderBy(derivative => derivative.Kind).ThenBy(derivative => derivative.CreatedAt).Select(ToDerivativeSummary).ToArray(),
            image.ConcurrencyToken);

    private PhotographyUploadFileResultDto ToFileResult(PhotographyUploadFileOutcome outcome, IReadOnlyDictionary<Guid, ArtifactImage> imagesById)
    {
        var image = outcome.ArtifactImageId.HasValue && imagesById.TryGetValue(outcome.ArtifactImageId.Value, out var resolvedImage)
            ? ToImageSummary(resolvedImage)
            : null;

        return new PhotographyUploadFileResultDto(
            outcome.ClientFileOrdinal,
            outcome.OriginalFilename,
            outcome.Status,
            outcome.ArtifactImageId,
            outcome.StaffFacingMessage,
            image);
    }

    private static ArtifactImageDerivativeSummaryDto ToDerivativeSummary(ArtifactImageDerivative derivative) =>
        new(
            derivative.ArtifactImageDerivativeId,
            derivative.Kind,
            derivative.ContentType,
            derivative.FileSizeBytes,
            derivative.PixelWidth,
            derivative.PixelHeight,
            derivative.CreatedAt);
}

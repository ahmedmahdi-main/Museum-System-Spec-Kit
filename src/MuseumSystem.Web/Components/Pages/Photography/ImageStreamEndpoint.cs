using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;

namespace MuseumSystem.Web.Components.Pages.Photography;

public static class ImageStreamEndpoint
{
    public const string Route = "/photography/images/{artifactImageId:guid}/{rendition}";

    public static IEndpointRouteBuilder MapPhotographyImageStreamEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Route, StreamImageAsync)
            .RequireAuthorization(PermissionNames.PhotographyView)
            .WithName("PhotographyImageStream");

        return endpoints;
    }

    private static async Task<IResult> StreamImageAsync(
        Guid artifactImageId,
        string rendition,
        ViewArtifactImagesUseCase useCase,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PhotographyImageRendition>(rendition, ignoreCase: true, out var requestedRendition)
            || !Enum.IsDefined(requestedRendition))
        {
            return Results.NotFound();
        }

        var result = await useCase.ReadArtifactImageRendition(
            new ReadArtifactImageRenditionQuery(artifactImageId, requestedRendition),
            cancellationToken);
        if (!result.Succeeded)
        {
            return Results.Forbid();
        }

        var image = result.Value;
        if (image is null || image.Status == PhotographyImageStreamStatus.NotFound)
        {
            return Results.NotFound();
        }

        if (image.Status == PhotographyImageStreamStatus.Unavailable || image.Content is null)
        {
            return Results.Problem(
                title: "Image unavailable",
                detail: "The requested museum image is temporarily unavailable.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Stream(
            image.Content,
            image.ContentType ?? "application/octet-stream",
            enableRangeProcessing: false);
    }
}

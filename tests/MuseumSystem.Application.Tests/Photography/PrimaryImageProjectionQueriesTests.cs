using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class PrimaryImageProjectionQueriesTests
{
    [Theory]
    [InlineData()]
    [InlineData(PermissionNames.PhotographyManage)]
    [InlineData(PermissionNames.PhotographyUpload)]
    [InlineData(PermissionNames.PhotographyRequest)]
    [InlineData(PermissionNames.PhotographyDelete)]
    public async Task Projection_requires_exact_photography_view_permission(params string[] permissions)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SetPrimaryArtifactImageUseCaseTests.SeedImage(db);
        SeedPrimaryState(db, artifact, image);
        await db.SaveChangesAsync();
        var queries = NewQueries(db, permissions);

        var result = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(artifact.ArtifactId));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
    }

    [Fact]
    public async Task Missing_state_and_null_primary_return_no_primary_without_creating_state_rows()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var missingStateArtifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var nullPrimaryArtifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        db.ArtifactPhotographyStates.Add(ArtifactPhotographyState.Create(nullPrimaryArtifact.ArtifactId));
        await db.SaveChangesAsync();
        var stateCountBefore = await db.ArtifactPhotographyStates.CountAsync();
        var queries = NewQueries(db, [PermissionNames.PhotographyView]);

        var missingState = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(missingStateArtifact.ArtifactId));
        var nullPrimary = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(nullPrimaryArtifact.ArtifactId));

        Assert.True(missingState.Succeeded);
        Assert.Null(missingState.Value);
        Assert.True(nullPrimary.Succeeded);
        Assert.Null(nullPrimary.Value);
        Assert.Equal(stateCountBefore, await db.ArtifactPhotographyStates.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Authoritative_available_primary_returns_safe_metadata_and_opaque_derivative_references()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, set, image) = UpdateArtifactImageMetadataUseCaseTests.SeedAvailableImage(db, addDerivatives: true);
        image.UpdateCaption("Display face");
        SeedPrimaryState(db, artifact, image);
        await db.SaveChangesAsync();
        var queries = NewQueries(db, [PermissionNames.PhotographyView]);

        var result = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(artifact.ArtifactId));

        Assert.True(result.Succeeded);
        var projection = result.Value!;
        Assert.Equal(artifact.ArtifactId, projection.ArtifactId);
        Assert.Equal(image.ArtifactImageId, projection.ArtifactImageId);
        Assert.Equal("Display face", projection.Caption);
        Assert.Equal(set.Purpose, projection.PhotographyPurpose);
        Assert.Equal(set.PhotographyDate, projection.PhotographyDate);
        Assert.Equal(set.PhotographerUserId, projection.PhotographerUserId);
        Assert.Equal(image.PixelWidth, projection.PixelWidth);
        Assert.Equal(image.PixelHeight, projection.PixelHeight);
        Assert.Equal(new PhotographyImageAccessReferenceDto(image.ArtifactImageId, PhotographyImageRendition.Thumbnail), projection.Thumbnail);
        Assert.Equal(new PhotographyImageAccessReferenceDto(image.ArtifactImageId, PhotographyImageRendition.Preview), projection.Preview);
        AssertNoStorageInternals(typeof(PrimaryImageProjectionDto), typeof(PhotographyImageAccessReferenceDto));
        var serialized = JsonSerializer.Serialize(projection);
        Assert.DoesNotContain("artifact-images/", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ObjectKey", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Projection_uses_only_authoritative_primary_and_does_not_fallback_to_other_images()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var availableButNotPrimary = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var queries = NewQueries(db, [PermissionNames.PhotographyView]);

        var result = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(artifact.ArtifactId));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value);
        Assert.NotEqual(Guid.Empty, availableButNotPrimary.ArtifactImageId);
    }

    [Theory]
    [InlineData(ArtifactImageStatus.DeletePending)]
    [InlineData(ArtifactImageStatus.Deleted)]
    public async Task Non_available_authoritative_primary_is_not_exposed_and_no_fallback_is_selected(ArtifactImageStatus status)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var nonAvailablePrimary = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set, status);
        var fallback = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        SeedPrimaryState(db, artifact, nonAvailablePrimary);
        await db.SaveChangesAsync();
        var queries = NewQueries(db, [PermissionNames.PhotographyView]);

        var result = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(artifact.ArtifactId));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value);
        Assert.NotEqual(nonAvailablePrimary.ArtifactImageId, fallback.ArtifactImageId);
    }

    [Fact]
    public async Task Missing_derivative_metadata_returns_null_access_references_without_binary_duplication()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SetPrimaryArtifactImageUseCaseTests.SeedImage(db);
        SeedPrimaryState(db, artifact, image);
        await db.SaveChangesAsync();
        var queries = NewQueries(db, [PermissionNames.PhotographyView]);

        var result = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(artifact.ArtifactId));

        Assert.True(result.Succeeded);
        Assert.Null(result.Value!.Thumbnail);
        Assert.Null(result.Value.Preview);
        AssertNoStorageInternals(typeof(PrimaryImageProjectionDto));
        Assert.DoesNotContain(typeof(PrimaryImageProjectionDto).GetProperties(), property =>
            property.PropertyType == typeof(byte[])
            || typeof(Stream).IsAssignableFrom(property.PropertyType)
            || property.Name.Contains("Base64", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Batch_projection_returns_mapping_for_requested_artifacts_with_one_bounded_database_flow()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifactA, imageA) = SetPrimaryArtifactImageUseCaseTests.SeedImage(db);
        var (artifactB, imageB) = SetPrimaryArtifactImageUseCaseTests.SeedImage(db);
        var artifactWithoutPrimary = PhotographyRequestApplicationTestHost.AddArtifact(db);
        SeedPrimaryState(db, artifactA, imageA);
        SeedPrimaryState(db, artifactB, imageB);
        await db.SaveChangesAsync();
        var queries = NewQueries(db, [PermissionNames.PhotographyView]);

        var result = await queries.GetPrimaryImagesForArtifacts(new PrimaryImagesForArtifactsQuery([
            artifactA.ArtifactId,
            artifactB.ArtifactId,
            artifactWithoutPrimary.ArtifactId]));

        Assert.True(result.Succeeded);
        Assert.Equal(imageA.ArtifactImageId, result.Value![artifactA.ArtifactId]!.ArtifactImageId);
        Assert.Equal(imageB.ArtifactImageId, result.Value[artifactB.ArtifactId]!.ArtifactImageId);
        Assert.Null(result.Value[artifactWithoutPrimary.ArtifactId]);
    }

    [Fact]
    public async Task Projection_validation_rejects_empty_artifact_ids()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var queries = NewQueries(db, [PermissionNames.PhotographyView]);

        var single = await queries.GetPrimaryImageForArtifact(new PrimaryImageForArtifactQuery(Guid.Empty));
        var batch = await queries.GetPrimaryImagesForArtifacts(new PrimaryImagesForArtifactsQuery([Guid.Empty]));

        Assert.Contains(single.ValidationIssues, issue => issue.Code == "Artifact.Required");
        Assert.Contains(batch.ValidationIssues, issue => issue.Code == "Artifact.Required");
    }

    [Fact]
    public void Projection_query_constructor_has_no_storage_dependency()
    {
        var constructorParameterTypes = typeof(PrimaryImageProjectionQueries)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain(constructorParameterTypes, name => name.Contains("Storage", StringComparison.OrdinalIgnoreCase));
        AssertNoStorageInternals(typeof(PrimaryImageProjectionDto), typeof(PrimaryImageForArtifactQuery), typeof(PrimaryImagesForArtifactsQuery));
    }

    private static PrimaryImageProjectionQueries NewQueries(
        MuseumDbContext db,
        IReadOnlyCollection<string> permissions) =>
        new(db, new FakeCurrentActorPermissionChecker(permissions));

    private static ArtifactPhotographyState SeedPrimaryState(
        MuseumDbContext db,
        Artifact artifact,
        ArtifactImage image)
    {
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        return state;
    }

    private static void AssertNoStorageInternals(params Type[] types)
    {
        var forbiddenFragments = new[] { "ObjectKey", "Bucket", "Endpoint", "Presigned", "Minio", "Binary", "Base64" };
        var memberNames = types
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(property => property.Name)
            .ToArray();

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.ArtifactRegistry;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.ArtifactRegistry;

public sealed class CreateArtifactUseCaseTests
{
    [Fact]
    public async Task Create_category_rejects_duplicate_category_code()
    {
        await using var db = CreateDbContext();
        var useCases = new CategoryUseCases(db);

        var first = await useCases.CreateCategory(new CreateCategoryRequest("A", "فئة", null));
        var duplicate = await useCases.CreateCategory(new CreateCategoryRequest("a", "فئة أخرى", null));

        Assert.True(first.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Contains(duplicate.ValidationIssues, issue => issue.Code == "CategoryCode.Duplicate");
    }

    [Fact]
    public async Task Create_artifact_sets_museum_number_from_category_code_and_item_number()
    {
        await using var db = CreateDbContext();
        var categoryUseCases = new CategoryUseCases(db);
        var locationUseCases = new LocationUseCases(db);
        var artifactUseCases = new ArtifactWriteUseCases(db);

        var category = await categoryUseCases.CreateCategory(new CreateCategoryRequest("TXT", "نسيج", null));
        var location = await locationUseCases.CreateLocation(new CreateLocationRequest("خزن 1", LocationType.Storage));

        var result = await artifactUseCases.CreateArtifact(new CreateArtifactRequest(category.Value!.CategoryId, 12, "وصف أساسي", location.Value!.LocationId));

        Assert.True(result.Succeeded);
        Assert.Equal("TXT-12", result.Value!.MuseumNumber);
        Assert.Equal(location.Value.LocationId, result.Value.CurrentLocationId);
        Assert.Equal(location.Value.LocationId, result.Value.LastKnownStorageLocationId);
    }

    [Fact]
    public async Task Create_artifact_rejects_blank_basic_description_without_throwing_or_persisting()
    {
        await using var db = CreateDbContext();
        var categoryUseCases = new CategoryUseCases(db);
        var locationUseCases = new LocationUseCases(db);
        var artifactUseCases = new ArtifactWriteUseCases(db);

        var category = await categoryUseCases.CreateCategory(new CreateCategoryRequest("TXT", "نسيج", null));
        var location = await locationUseCases.CreateLocation(new CreateLocationRequest("خزن 1", LocationType.Storage));

        var exception = await Record.ExceptionAsync(async () =>
            await artifactUseCases.CreateArtifact(new CreateArtifactRequest(category.Value!.CategoryId, 12, "   ", location.Value!.LocationId)));
        var result = await artifactUseCases.CreateArtifact(new CreateArtifactRequest(category.Value!.CategoryId, 12, "   ", location.Value!.LocationId));

        Assert.Null(exception);
        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Artifact.BasicDescriptionRequired" && issue.Message == "الوصف الأساسي مطلوب.");
        Assert.Equal(0, await db.Artifacts.CountAsync());
    }

    [Fact]
    public async Task Create_artifact_rejects_missing_required_inputs_before_domain_creation()
    {
        await using var db = CreateDbContext();
        var artifactUseCases = new ArtifactWriteUseCases(db);

        var result = await artifactUseCases.CreateArtifact(new CreateArtifactRequest(Guid.Empty, 0, string.Empty, Guid.Empty));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Category.Required");
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ItemNumber.Positive");
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Location.Required");
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Artifact.BasicDescriptionRequired");
        Assert.Equal(0, await db.Artifacts.CountAsync());
    }

    [Fact]
    public async Task Create_artifact_rejects_duplicate_item_number_inside_same_category()
    {
        await using var db = CreateDbContext();
        var categoryUseCases = new CategoryUseCases(db);
        var locationUseCases = new LocationUseCases(db);
        var artifactUseCases = new ArtifactWriteUseCases(db);

        var category = await categoryUseCases.CreateCategory(new CreateCategoryRequest("TXT", "نسيج", null));
        var location = await locationUseCases.CreateLocation(new CreateLocationRequest("خزن 1", LocationType.Storage));

        var first = await artifactUseCases.CreateArtifact(new CreateArtifactRequest(category.Value!.CategoryId, 12, "وصف أساسي", location.Value!.LocationId));
        var duplicate = await artifactUseCases.CreateArtifact(new CreateArtifactRequest(category.Value.CategoryId, 12, "وصف آخر", location.Value.LocationId));

        Assert.True(first.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Contains(duplicate.ValidationIssues, issue => issue.Code == "MuseumNumber.Duplicate");
    }

    [Fact]
    public async Task Update_category_rejects_code_change_after_artifact_exists()
    {
        await using var db = CreateDbContext();
        var categoryUseCases = new CategoryUseCases(db);
        var locationUseCases = new LocationUseCases(db);
        var artifactUseCases = new ArtifactWriteUseCases(db);

        var category = await categoryUseCases.CreateCategory(new CreateCategoryRequest("TXT", "Textiles", null));
        var location = await locationUseCases.CreateLocation(new CreateLocationRequest("Shelf 1", LocationType.Storage));
        var artifact = await artifactUseCases.CreateArtifact(new CreateArtifactRequest(category.Value!.CategoryId, 12, "Basic description", location.Value!.LocationId));

        var result = await categoryUseCases.UpdateCategory(new UpdateCategoryRequest(category.Value.CategoryId, "DOC", "Documents", null));

        Assert.True(artifact.Succeeded);
        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "CategoryCode.InUse");
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }
}

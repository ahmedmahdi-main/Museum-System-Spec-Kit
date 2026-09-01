using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MuseumSystem.Domain.Modules.Photography;
using Npgsql;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class ArtifactPhotographyStatePersistenceTests(PostgresPhotographyTestFixture fixture)
{
    [Fact]
    public async Task Photography_state_requires_an_existing_artifact()
    {
        await using var context = fixture.CreateContext();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactPhotographyStates" (
                "ArtifactId", "PrimaryImageId", "ConcurrencyToken", "UpdatedAt", "UpdatedByUserId")
            values ({0}, null, 0, now(), 'manager')
            """, Guid.NewGuid()));
    }

    [Fact]
    public async Task Only_one_photography_state_row_is_allowed_per_artifact()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "SO");
        context.ArtifactPhotographyStates.Add(ArtifactPhotographyState.Create(artifact.ArtifactId));
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactPhotographyStates" (
                "ArtifactId", "PrimaryImageId", "ConcurrencyToken", "UpdatedAt", "UpdatedByUserId")
            values ({0}, null, 0, now(), 'manager')
            """, artifact.ArtifactId));
    }

    [Fact]
    public async Task Photography_state_persists_successfully_with_a_null_primary_image()
    {
        Guid artifactId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "NP");
            seed.ArtifactPhotographyStates.Add(ArtifactPhotographyState.Create(artifact.ArtifactId));
            await seed.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);

        Assert.Null(reloaded.PrimaryImageId);
    }

    [Fact]
    public async Task Photography_state_persists_a_primary_image_belonging_to_the_same_artifact()
    {
        Guid artifactId;
        Guid imageId;
        await using (var seed = fixture.CreateContext())
        {
            var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(seed, "SP");
            var set = await PhotographyPersistenceTestData.SeedSetAsync(seed, artifact.ArtifactId);
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifact.ArtifactId, set.PhotographySetId, "original/same-artifact-primary");
            var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
            state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
            seed.ArtifactPhotographyStates.Add(state);
            await seed.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            imageId = image.ArtifactImageId;
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactPhotographyStates.SingleAsync(state => state.ArtifactId == artifactId);

        Assert.Equal(imageId, reloaded.PrimaryImageId);
        Assert.Equal(1, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task Postgresql_rejects_a_primary_image_belonging_to_a_different_artifact()
    {
        await using var context = fixture.CreateContext();
        var artifactA = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "DA");
        var artifactB = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "DB");
        var setB = await PhotographyPersistenceTestData.SeedSetAsync(context, artifactB.ArtifactId);
        var imageB = await PhotographyPersistenceTestData.SeedImageAsync(context, artifactB.ArtifactId, setB.PhotographySetId, "original/different-artifact-primary");
        var stateA = ArtifactPhotographyState.Create(artifactA.ArtifactId);
        context.ArtifactPhotographyStates.Add(stateA);
        await context.SaveChangesAsync();

        context.Entry(stateA).Property(nameof(ArtifactPhotographyState.PrimaryImageId)).CurrentValue = imageB.ArtifactImageId;
        context.Entry(stateA).Property(nameof(ArtifactPhotographyState.PrimaryImageId)).IsModified = true;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Postgresql_rejects_a_primary_image_that_does_not_exist()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "MI");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactPhotographyStates" (
                "ArtifactId", "PrimaryImageId", "ConcurrencyToken", "UpdatedAt", "UpdatedByUserId")
            values ({0}, {1}, 0, now(), 'manager')
            """, artifact.ArtifactId, Guid.NewGuid()));
    }

    [Fact]
    public void Entity_model_marks_photography_state_concurrency_token_as_a_concurrency_token()
    {
        using var context = fixture.CreateContext();

        var entityType = context.Model.FindEntityType(typeof(ArtifactPhotographyState));
        var concurrencyProperty = entityType?.FindProperty(nameof(ArtifactPhotographyState.ConcurrencyToken));

        Assert.NotNull(concurrencyProperty);
        Assert.True(concurrencyProperty!.IsConcurrencyToken);
    }

    [Fact]
    public async Task Artifact_images_table_has_no_independent_is_primary_authority_column()
    {
        await using var context = fixture.CreateContext();

        var columnCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value"
            from information_schema.columns
            where table_schema = 'museum'
              and table_name = 'ArtifactImages'
              and column_name in ('IsPrimary', 'Primary', 'PrimaryImage')
            """).SingleAsync();

        Assert.Equal(0, columnCount);
    }

    [Fact]
    public async Task Applied_photography_migration_defines_the_photography_state_table_and_its_foreign_keys()
    {
        await using var context = fixture.CreateContext();

        var stateTableExists = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value"
            from information_schema.tables
            where table_schema = 'museum' and table_name = 'ArtifactPhotographyStates'
            """).SingleAsync();
        var primaryImageForeignKeyExists = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value"
            from pg_constraint c
            join pg_namespace n on n.oid = c.connamespace
            where n.nspname = 'museum'
              and c.conname = 'FK_ArtifactPhotographyStates_ArtifactImages_PrimaryImageId_Art~'
            """).SingleAsync();

        Assert.Equal(1, stateTableExists);
        Assert.Equal(1, primaryImageForeignKeyExists);
    }
}

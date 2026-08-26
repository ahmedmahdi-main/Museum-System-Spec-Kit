using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MuseumSystem.Integration.Tests.Photography;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresPhotographyCollection : ICollectionFixture<PostgresPhotographyTestFixture>
{
    public const string Name = "PostgreSQL Photography";
}

public sealed class PostgresPhotographyTestFixture : IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable = "MUSEUMSYSTEM_POSTGRES_TEST_CONNECTION";
    private const string TestUsername = "museum_app";
    private const string TestPassword = "photography_test_password";

    private PostgreSqlContainer? _container;
    private string? _databaseName;
    private string? _masterConnectionString;
    private bool _databaseCreated;

    public DbContextOptions<MuseumDbContext> Options { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            var configured = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
            var baseConnectionString = string.IsNullOrWhiteSpace(configured)
                ? await StartContainerAsync()
                : configured;

            var builder = new NpgsqlConnectionStringBuilder(baseConnectionString);
            _databaseName = $"museum_feature003_{Guid.NewGuid():N}";
            _masterConnectionString = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
            {
                Database = string.IsNullOrWhiteSpace(builder.Database) ? "postgres" : builder.Database
            }.ConnectionString;
            var testConnectionString = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
            {
                Database = _databaseName
            }.ConnectionString;

            await CreateDatabaseAsync(_masterConnectionString, _databaseName);
            _databaseCreated = true;

            Options = new DbContextOptionsBuilder<MuseumDbContext>()
                .UseNpgsql(testConnectionString)
                .Options;

            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "PostgreSQL Photography integration tests require a reachable PostgreSQL instance. " +
                $"Set {ConnectionStringEnvironmentVariable} to reuse an existing test database server, " +
                "or ensure Docker is running so Testcontainers can start a disposable PostgreSQL container. " +
                "These tests are required for Photography migrations, constraints, foreign keys, uniqueness, and concurrency coverage.",
                ex);
        }
    }

    public MuseumDbContext CreateContext() => new(Options);

    public async Task DisposeAsync()
    {
        if (_databaseCreated && !string.IsNullOrWhiteSpace(_databaseName) && !string.IsNullOrWhiteSpace(_masterConnectionString))
        {
            await DropDatabaseAsync(_masterConnectionString, _databaseName);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private static async Task CreateDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string masterConnectionString, string databaseName)
    {
        await using var connection = new NpgsqlConnection(masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> StartContainerAsync()
    {
        _container = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("postgres")
            .WithUsername(TestUsername)
            .WithPassword(TestPassword)
            .Build();

        await _container.StartAsync();
        return _container.GetConnectionString();
    }
}

internal static class PhotographyPersistenceTestData
{
    public static async Task<Artifact> SeedArtifactAsync(MuseumDbContext context, string prefix = "PH")
    {
        var category = ArtifactCategory.Create($"{prefix}{Guid.NewGuid():N}"[..8], "Photography category");
        var location = Location.Create($"Storage {Guid.NewGuid():N}"[..24], LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Photography artifact", location);

        context.ArtifactCategories.Add(category);
        context.Locations.Add(location);
        context.Artifacts.Add(artifact);
        await context.SaveChangesAsync();

        return artifact;
    }

    public static async Task<PhotographySet> SeedSetAsync(MuseumDbContext context, Guid artifactId)
    {
        var set = PhotographySet.Create(
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 24),
            "photographer");
        context.PhotographySets.Add(set);
        await context.SaveChangesAsync();

        return set;
    }

    public static async Task<ArtifactImage> SeedImageAsync(MuseumDbContext context, Guid artifactId, Guid photographySetId, string objectKey)
    {
        var image = ArtifactImage.Create(
            artifactId,
            photographySetId,
            ImageStorageObjectKey.Create(objectKey),
            "artifact.png",
            "image/png",
            1024,
            640,
            480,
            "uploader",
            DateTimeOffset.UtcNow);
        context.ArtifactImages.Add(image);
        await context.SaveChangesAsync();

        return image;
    }
}

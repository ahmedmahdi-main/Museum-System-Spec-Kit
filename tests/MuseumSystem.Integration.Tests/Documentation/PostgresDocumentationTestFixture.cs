using Microsoft.EntityFrameworkCore;
using MuseumSystem.Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MuseumSystem.Integration.Tests.Documentation;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresDocumentationCollection : ICollectionFixture<PostgresDocumentationTestFixture>
{
    public const string Name = "PostgreSQL Documentation";
}

public sealed class PostgresDocumentationTestFixture : IAsyncLifetime
{
    private const string ConnectionStringEnvironmentVariable = "MUSEUMSYSTEM_POSTGRES_TEST_CONNECTION";
    private const string TestUsername = "museum_app";
    private const string TestPassword = "phasea_test_password";

    private PostgreSqlContainer? _container;
    private string? _databaseName;
    private string? _masterConnectionString;
    private string? _testConnectionString;
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
            _databaseName = $"museum_feature002_{Guid.NewGuid():N}";
            _masterConnectionString = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
            {
                Database = string.IsNullOrWhiteSpace(builder.Database) ? "postgres" : builder.Database
            }.ConnectionString;
            _testConnectionString = new NpgsqlConnectionStringBuilder(builder.ConnectionString)
            {
                Database = _databaseName
            }.ConnectionString;

            await CreateDatabaseAsync(_masterConnectionString, _databaseName);
            _databaseCreated = true;

            Options = new DbContextOptionsBuilder<MuseumDbContext>()
                .UseNpgsql(_testConnectionString)
                .Options;

            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "PostgreSQL Documentation integration tests require a reachable PostgreSQL instance. " +
                $"Set {ConnectionStringEnvironmentVariable} to reuse an existing test database server, " +
                "or ensure Docker is running so Testcontainers can start a disposable PostgreSQL container. " +
                "These tests are required for migrations, JSONB, constraints, foreign keys, concurrency, and Feature 001 regression coverage.",
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

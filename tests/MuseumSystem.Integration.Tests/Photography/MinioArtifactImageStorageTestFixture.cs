using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using MuseumSystem.Infrastructure.Photography.Storage;

namespace MuseumSystem.Integration.Tests.Photography;

public sealed class MinioArtifactImageStorageTestFixture : IAsyncLifetime
{
    private const int MinioPort = 9000;
    private const string EndpointEnvironmentVariable = "MUSEUMSYSTEM_MINIO_TEST_ENDPOINT";
    private const string AccessKeyEnvironmentVariable = "MUSEUMSYSTEM_MINIO_TEST_ACCESS_KEY";
    private const string SecretKeyEnvironmentVariable = "MUSEUMSYSTEM_MINIO_TEST_SECRET_KEY";
    private const string BucketEnvironmentVariable = "MUSEUMSYSTEM_MINIO_TEST_BUCKET";
    private const string UseTlsEnvironmentVariable = "MUSEUMSYSTEM_MINIO_TEST_USE_TLS";
    private const string DefaultAccessKey = "minioadmin";
    private const string DefaultSecretKey = "minioadmin";

    private readonly List<string> objectKeys = [];
    private IContainer? container;
    private IMinioClient? adminClient;

    public MinioArtifactImageStorageOptions Options { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            var configuredEndpoint = Environment.GetEnvironmentVariable(EndpointEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configuredEndpoint))
            {
                container = new ContainerBuilder("minio/minio:latest")
                    .WithEnvironment("MINIO_ROOT_USER", DefaultAccessKey)
                    .WithEnvironment("MINIO_ROOT_PASSWORD", DefaultSecretKey)
                    .WithPortBinding(MinioPort, true)
                    .WithCommand("server", "/data", "--address", ":9000")
                    .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(MinioPort))
                    .Build();

                await container.StartAsync();
                configuredEndpoint = $"http://localhost:{container.GetMappedPublicPort(MinioPort)}";
            }

            Options = new MinioArtifactImageStorageOptions
            {
                Provider = "Minio",
                Endpoint = configuredEndpoint,
                BucketName = Environment.GetEnvironmentVariable(BucketEnvironmentVariable) ?? $"museum-feature003-{Guid.NewGuid():N}",
                AccessKey = Environment.GetEnvironmentVariable(AccessKeyEnvironmentVariable) ?? DefaultAccessKey,
                SecretKey = Environment.GetEnvironmentVariable(SecretKeyEnvironmentVariable) ?? DefaultSecretKey,
                Region = "us-east-1",
                UseTls = bool.TryParse(Environment.GetEnvironmentVariable(UseTlsEnvironmentVariable), out var useTls) && useTls,
                RequestTimeoutSeconds = 10
            };

            adminClient = new MinioClient()
                .WithEndpoint(NormalizeEndpoint(Options.Endpoint))
                .WithCredentials(Options.AccessKey, Options.SecretKey)
                .WithSSL(Options.UseTls)
                .Build();

            var bucketExists = await adminClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(Options.BucketName));
            if (!bucketExists)
            {
                await adminClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(Options.BucketName));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "MinIO integration tests require a reachable private MinIO test bucket. " +
                $"Set {EndpointEnvironmentVariable}, {AccessKeyEnvironmentVariable}, {SecretKeyEnvironmentVariable}, and optionally {BucketEnvironmentVariable}, " +
                "or ensure Docker is running so Testcontainers can start an isolated MinIO instance.",
                ex);
        }
    }

    public MinioArtifactImageStorage CreateStorage() => new(Microsoft.Extensions.Options.Options.Create(Options));

    public string CreateObjectKey(string suffix)
    {
        var objectKey = $"artifact-images/integration/{Guid.NewGuid():N}/{suffix}";
        objectKeys.Add(objectKey);
        return objectKey;
    }

    public async Task DisposeAsync()
    {
        if (adminClient is not null)
        {
            foreach (var objectKey in objectKeys.Distinct(StringComparer.Ordinal))
            {
                try
                {
                    await adminClient.RemoveObjectAsync(new RemoveObjectArgs()
                        .WithBucket(Options.BucketName)
                        .WithObject(objectKey));
                }
                catch
                {
                    // Best-effort cleanup of test-owned keys only.
                }
            }
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }

        return endpoint;
    }
}

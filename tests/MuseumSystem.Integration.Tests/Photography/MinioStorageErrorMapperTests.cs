using System.Net.Http;
using System.Reflection;
using Minio.Exceptions;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Infrastructure.Photography.Storage;

namespace MuseumSystem.Integration.Tests.Photography;

public sealed class MinioStorageErrorMapperTests
{
    public static TheoryData<Exception, string> RetryableOperationalFailures() =>
        new()
        {
            { CreateMinioException("Minio.Exceptions.ConnectionException", "Connection failed for http://127.0.0.1:9000/artifact-images/secret.jpg"), "Storage.RetryableFailure" },
            { new HttpRequestException("Connection failed for http://127.0.0.1:9000/artifact-images/secret.jpg"), "Storage.RetryableFailure" },
            { new IOException("Could not read path C:\\storage\\secret.jpg"), "Storage.RetryableFailure" },
            { new TimeoutException("Timed out contacting endpoint http://127.0.0.1:9000"), "Storage.RetryableFailure" },
            { new InvalidOperationException("Unexpected provider failure for artifact-images/secret.jpg"), "Storage.UnknownFailure" }
        };

    [Theory]
    [MemberData(nameof(RetryableOperationalFailures))]
    public void Operational_failures_map_to_retryable_structured_failure_without_leaking_provider_text(Exception exception, string expectedCode)
    {
        Assert.True(MinioStorageErrorMapper.TryMap(exception, out var failure));

        Assert.Equal(ArtifactImageStorageResultKind.RetryableFailure, failure.Kind);
        Assert.Equal(expectedCode, failure.Code);
        Assert.Equal("Image storage is temporarily unavailable. Please try again.", failure.StaffFacingMessage);
        AssertNoProviderLeak(failure.StaffFacingMessage);
        AssertNoProviderLeak(failure.OperationalSummary!);
    }

    [Fact]
    public void Operation_cancellation_is_not_mapped()
    {
        Assert.False(MinioStorageErrorMapper.TryMap(new OperationCanceledException("request canceled"), out _));
    }

    [Fact]
    public void Provider_timeout_cancellation_maps_to_retryable_only_when_caller_token_was_not_canceled()
    {
        Assert.True(MinioStorageErrorMapper.TryMap(new TaskCanceledException("provider timeout"), CancellationToken.None, out var failure));
        Assert.Equal(ArtifactImageStorageResultKind.RetryableFailure, failure.Kind);

        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.False(MinioStorageErrorMapper.TryMap(new TaskCanceledException("caller canceled"), source.Token, out _));
    }

    [Theory]
    [InlineData("Minio.Exceptions.InvalidBucketNameException")]
    [InlineData("Minio.Exceptions.BucketNotFoundException")]
    public void Configuration_failures_map_to_unauthorized_or_misconfigured(string exceptionTypeName)
    {
        var exception = CreateMinioException(
            exceptionTypeName,
            "Invalid bucket artifact-images at endpoint http://127.0.0.1:9000");

        Assert.True(MinioStorageErrorMapper.TryMap(exception, out var failure));

        Assert.Equal(ArtifactImageStorageResultKind.UnauthorizedOrMisconfigured, failure.Kind);
        Assert.Equal("Image storage is currently unavailable.", failure.StaffFacingMessage);
        AssertNoProviderLeak(failure.StaffFacingMessage);
        AssertNoProviderLeak(failure.OperationalSummary!);
    }

    private static Exception CreateMinioException(string typeName, string message)
    {
        var type = typeof(MinioException).Assembly.GetType(typeName, throwOnError: true)!;
        Exception? lastFailure = null;

        foreach (var constructor in type.GetConstructors().OrderBy(static candidate => candidate.GetParameters().Length))
        {
            var parameters = constructor.GetParameters();
            var arguments = parameters.Select(parameter => CreateArgument(parameter.ParameterType, message)).ToArray();

            try
            {
                return (Exception)constructor.Invoke(arguments);
            }
            catch (TargetInvocationException ex)
            {
                lastFailure = ex.InnerException ?? ex;
            }
            catch (ArgumentException ex)
            {
                lastFailure = ex;
            }
        }

        throw new InvalidOperationException($"Could not construct {typeName}.", lastFailure);
    }

    private static object? CreateArgument(Type parameterType, string message)
    {
        if (parameterType == typeof(string))
        {
            return message;
        }

        if (parameterType == typeof(Exception))
        {
            return new HttpRequestException(message);
        }

        if (parameterType == typeof(Uri))
        {
            return new Uri("http://127.0.0.1:9000/artifact-images/secret.jpg");
        }

        if (parameterType == typeof(TimeSpan))
        {
            return TimeSpan.FromSeconds(1);
        }

        if (parameterType.IsEnum)
        {
            return Activator.CreateInstance(parameterType);
        }

        return parameterType.IsValueType
            ? Activator.CreateInstance(parameterType)
            : null;
    }

    private static void AssertNoProviderLeak(string value)
    {
        Assert.DoesNotContain("MinIO", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("artifact-images", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", value, StringComparison.OrdinalIgnoreCase);
    }
}

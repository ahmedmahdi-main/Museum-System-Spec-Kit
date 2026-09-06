using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

public sealed class PhotographyInfrastructureBoundaryTests
{
    private static readonly RegexOptions BoundaryRegexOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly string[] ForbiddenAssemblyFragments =
    [
        "Minio",
        "AWSSDK.S3",
        "Amazon.S3",
        "Azure.Storage.Blobs",
        "Google.Cloud.Storage",
        "Testcontainers",
        "Docker.DotNet"
    ];

    private static readonly string[] ForbiddenProviderSourcePatterns =
    [
        @"^\s*using\s+Minio(\.|;)",
        @"\bMinioClient\b",
        @"\bIMinioClient\b",
        @"\bMinioException\b",
        @"\bConnectionException\b",
        @"\bBucketNotFoundException\b",
        @"\bAuthorizationException\b",
        @"\bAccessDeniedException\b",
        @"\bInvalidEndpointException\b",
        @"\bInvalidBucketNameException\b",
        @"\bAWSSDK\.S3\b",
        @"\bAmazon\.S3\b",
        @"\bAzure\.Storage\.Blobs\b",
        @"\bGoogle\.Cloud\.Storage\b"
    ];

    private static readonly string[] ForbiddenProviderConfigurationPatterns =
    [
        @"\bBucketName\b",
        @"\bAccessKey\b",
        @"\bSecretKey\b",
        @"\b(Minio|S3|Blob|ObjectStorage|StorageProvider)[A-Za-z0-9_]*Endpoint[A-Za-z0-9_]*\b",
        @"\bEndpoint(Uri|Url)\b",
        @"\bEndpoint\s*[=:]"
    ];

    private static readonly string[] ForbiddenPortabilityPatterns =
    [
        @"\b[A-Z]:[\\/]",
        @"@?""\\\\[^\\""\r\n]+\\",
        @"@?""/(var|srv|mnt|data|opt)(/|""|\s)",
        @"\bOperatingSystem\.IsWindows\s*\(",
        @"\bOperatingSystem\.IsLinux\s*\(",
        @"\bOSPlatform\.Windows\b",
        @"\bOSPlatform\.Linux\b",
        @"\bRuntimeInformation\.IsOSPlatform\s*\(",
        @"\bMono\.Unix\b",
        @"\bDocker\.DotNet\b",
        @"\bdocker-compose\b",
        @"\bDocker\b",
        @"\bTestcontainers\b",
        @"\bwsl\.exe\b",
        @"Windows Subsystem for Linux"
    ];

    [Fact]
    public void Domain_and_application_assemblies_reference_only_portable_layers_and_provider_neutral_dependencies()
    {
        var domainReferences = ReferencedAssemblyNames(typeof(PhotographyPurpose).Assembly);
        var applicationReferences = ReferencedAssemblyNames(typeof(CreatePhotographySetWithImagesUseCase).Assembly);

        Assert.DoesNotContain(domainReferences, reference => IsAssembly(reference, "MuseumSystem.Application"));
        Assert.DoesNotContain(domainReferences, reference => IsAssembly(reference, "MuseumSystem.Infrastructure"));
        Assert.DoesNotContain(domainReferences, reference => IsAssembly(reference, "MuseumSystem.Web"));

        Assert.Contains(applicationReferences, reference => IsAssembly(reference, "MuseumSystem.Domain"));
        Assert.DoesNotContain(applicationReferences, reference => IsAssembly(reference, "MuseumSystem.Infrastructure"));
        Assert.DoesNotContain(applicationReferences, reference => IsAssembly(reference, "MuseumSystem.Web"));

        AssertNoForbiddenFragments("MuseumSystem.Domain assembly references", domainReferences, ForbiddenAssemblyFragments);
        AssertNoForbiddenFragments("MuseumSystem.Application assembly references", applicationReferences, ForbiddenAssemblyFragments);
    }

    [Fact]
    public void Domain_and_application_project_files_keep_dependency_direction_and_provider_packages_out()
    {
        var root = RepositoryRoot();
        var domainProject = ReadProject(root, "src", "MuseumSystem.Domain", "MuseumSystem.Domain.csproj");
        var applicationProject = ReadProject(root, "src", "MuseumSystem.Application", "MuseumSystem.Application.csproj");

        AssertProjectDoesNotReference(domainProject, "MuseumSystem.Application.csproj", "MuseumSystem.Infrastructure.csproj", "MuseumSystem.Web.csproj");
        AssertProjectReferences(applicationProject, "MuseumSystem.Domain.csproj");
        AssertProjectDoesNotReference(applicationProject, "MuseumSystem.Infrastructure.csproj", "MuseumSystem.Web.csproj");

        AssertProjectDoesNotPackageReference(domainProject, ForbiddenAssemblyFragments);
        AssertProjectDoesNotPackageReference(applicationProject, ForbiddenAssemblyFragments);
    }

    [Fact]
    public void Domain_and_application_source_do_not_depend_on_concrete_storage_providers_or_deployment_hosts()
    {
        var files = BoundarySourceFiles(RepositoryRoot()).ToArray();

        Assert.NotEmpty(files);
        AssertNoMatch(files, ForbiddenProviderSourcePatterns);
        AssertNoMatch(files, ForbiddenProviderConfigurationPatterns);
        AssertNoMatch(files, ForbiddenPortabilityPatterns);
    }

    [Fact]
    public void Public_storage_interface_signatures_expose_only_domain_application_and_system_contracts()
    {
        var forbiddenNameFragments = ForbiddenAssemblyFragments.Concat([
            "MuseumSystem.Infrastructure",
            "MuseumSystem.Web",
            "Bucket",
            "Endpoint",
            "Client",
            "Exception"
        ]).ToArray();

        foreach (var method in typeof(IArtifactImageStorage).GetMethods())
        {
            AssertBoundaryType(method.ReturnType, $"IArtifactImageStorage.{method.Name} return type", forbiddenNameFragments);
            foreach (var parameter in method.GetParameters())
            {
                AssertBoundaryType(parameter.ParameterType, $"IArtifactImageStorage.{method.Name} parameter {parameter.Name}", forbiddenNameFragments);
            }
        }
    }

    [Fact]
    public void Storage_result_contracts_remain_provider_neutral_while_allowing_logical_object_identity()
    {
        var resultTypes = new[]
        {
            typeof(ArtifactImageStorageFailure),
            typeof(ArtifactImageStorageWriteResult),
            typeof(ArtifactImageStorageStatResult),
            typeof(ArtifactImageStorageReadResult),
            typeof(ArtifactImageStorageDeleteResult),
            typeof(ArtifactImageObjectsDeleteResult),
            typeof(ArtifactImageShortLivedReadAccessResult),
            typeof(ArtifactImageStoredObjectMetadata),
            typeof(ImageStorageObjectKey)
        };

        foreach (var type in resultTypes)
        {
            AssertPublicContractSurface(type, type.FullName ?? type.Name, ForbiddenAssemblyFragments.Concat([
                "MuseumSystem.Infrastructure",
                "MuseumSystem.Web",
                "Bucket",
                "Endpoint",
                "MinioClient",
                "Exception"
            ]).ToArray());
        }
    }

    [Fact]
    public void Storage_health_classifier_depends_only_on_provider_neutral_storage_contracts()
    {
        var root = RepositoryRoot();
        var file = new SourceFile(
            RelativePath(root, Path.Combine(root.FullName, "src", "MuseumSystem.Application", "Modules", "Photography", "ArtifactImageStorageHealthService.cs")),
            File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Application", "Modules", "Photography", "ArtifactImageStorageHealthService.cs")));

        AssertNoMatch([file], ForbiddenProviderSourcePatterns);
        AssertNoMatch([file], ForbiddenProviderConfigurationPatterns);

        var publicConstructors = typeof(ArtifactImageStorageHealthService).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.All(publicConstructors, constructor => Assert.Empty(constructor.GetParameters()));
        AssertPublicContractSurface(typeof(ArtifactImageStorageHealthService), nameof(ArtifactImageStorageHealthService), ForbiddenAssemblyFragments.Concat([
            "MuseumSystem.Infrastructure",
            "MuseumSystem.Web"
        ]).ToArray());
    }

    [Fact]
    public void Infrastructure_is_the_allowed_home_for_minio_provider_details()
    {
        var root = RepositoryRoot();
        var infrastructureSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root.FullName, "src", "MuseumSystem.Infrastructure", "Photography"), "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.Contains("MinioArtifactImageStorage", infrastructureSource, StringComparison.Ordinal);
        Assert.Contains("MinioStorageErrorMapper", infrastructureSource, StringComparison.Ordinal);
        Assert.Contains("BucketName", infrastructureSource, StringComparison.Ordinal);
        Assert.Contains("Endpoint", infrastructureSource, StringComparison.Ordinal);
        Assert.Contains("AccessKey", infrastructureSource, StringComparison.Ordinal);
        Assert.Contains("SecretKey", infrastructureSource, StringComparison.Ordinal);
    }

    private static ProjectInspection ReadProject(DirectoryInfo root, params string[] segments)
    {
        var path = Path.Combine([root.FullName, .. segments]);
        var project = XDocument.Load(path);
        return new ProjectInspection(
            RelativePath(root, path),
            project.Descendants("ProjectReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray(),
            project.Descendants("PackageReference")
                .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray());
    }

    private static void AssertProjectReferences(ProjectInspection project, string expectedProjectFile) =>
        Assert.Contains(project.ProjectReferences, reference => reference.EndsWith(expectedProjectFile, StringComparison.OrdinalIgnoreCase));

    private static void AssertProjectDoesNotReference(ProjectInspection project, params string[] forbiddenProjectFiles)
    {
        foreach (var forbidden in forbiddenProjectFiles)
        {
            Assert.DoesNotContain(project.ProjectReferences, reference => reference.EndsWith(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertProjectDoesNotPackageReference(ProjectInspection project, IReadOnlyCollection<string> forbiddenFragments)
    {
        foreach (var forbidden in forbiddenFragments)
        {
            Assert.DoesNotContain(project.PackageReferences, package => package.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertNoForbiddenFragments(string scope, IEnumerable<string> values, IReadOnlyCollection<string> forbiddenFragments)
    {
        foreach (var forbidden in forbiddenFragments)
        {
            Assert.DoesNotContain(values, value => value.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AssertNoMatch(IEnumerable<SourceFile> files, IReadOnlyCollection<string> forbiddenPatterns)
    {
        foreach (var file in files)
        {
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.False(
                    Regex.IsMatch(file.Text, pattern, BoundaryRegexOptions | RegexOptions.Multiline),
                    $"Forbidden boundary dependency matched {pattern} in {file.RelativePath}.");
            }
        }
    }

    private static void AssertPublicContractSurface(Type type, string scope, IReadOnlyCollection<string> forbiddenFragments)
    {
        AssertBoundaryType(type, scope, forbiddenFragments);

        foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            foreach (var parameter in constructor.GetParameters())
            {
                AssertBoundaryType(parameter.ParameterType, $"{scope} constructor parameter {parameter.Name}", forbiddenFragments);
            }
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            AssertBoundaryType(property.PropertyType, $"{scope}.{property.Name}", forbiddenFragments);
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            AssertBoundaryType(method.ReturnType, $"{scope}.{method.Name} return type", forbiddenFragments);
            foreach (var parameter in method.GetParameters())
            {
                AssertBoundaryType(parameter.ParameterType, $"{scope}.{method.Name} parameter {parameter.Name}", forbiddenFragments);
            }
        }
    }

    private static void AssertBoundaryType(Type type, string scope, IReadOnlyCollection<string> forbiddenFragments)
    {
        var types = FlattenTypes(type).ToArray();
        foreach (var inspectedType in types)
        {
            var name = inspectedType.FullName ?? inspectedType.Name;
            var assemblyName = inspectedType.Assembly.GetName().Name ?? string.Empty;
            var allowed = IsAllowedBoundaryType(inspectedType);

            Assert.True(allowed, $"{scope} exposes non-boundary type {name} from {assemblyName}.");
            foreach (var forbidden in forbiddenFragments)
            {
                Assert.DoesNotContain(forbidden, name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(forbidden, assemblyName, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static IEnumerable<Type> FlattenTypes(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            foreach (var nested in FlattenTypes(type.GetElementType()!))
            {
                yield return nested;
            }

            yield break;
        }

        yield return type.IsGenericType ? type.GetGenericTypeDefinition() : type;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in FlattenTypes(argument))
            {
                yield return nested;
            }
        }
    }

    private static bool IsAllowedBoundaryType(Type type)
    {
        var namespaceName = type.Namespace ?? string.Empty;
        return namespaceName == "System" ||
               namespaceName.StartsWith("System.", StringComparison.Ordinal) ||
               namespaceName.StartsWith("MuseumSystem.Application", StringComparison.Ordinal) ||
               namespaceName.StartsWith("MuseumSystem.Domain", StringComparison.Ordinal);
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .ToArray();

    private static bool IsAssembly(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<SourceFile> BoundarySourceFiles(DirectoryInfo root)
    {
        foreach (var sourceRoot in new[]
        {
            Path.Combine(root.FullName, "src", "MuseumSystem.Domain"),
            Path.Combine(root.FullName, "src", "MuseumSystem.Application")
        })
        {
            foreach (var path in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(segment => segment is "bin" or "obj"))
                {
                    continue;
                }

                yield return new SourceFile(RelativePath(root, path), File.ReadAllText(path));
            }
        }
    }

    private static string RelativePath(DirectoryInfo root, string path) =>
        Path.GetRelativePath(root.FullName, path).Replace(Path.DirectorySeparatorChar, '/');

    private static DirectoryInfo RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new InvalidOperationException("Could not locate repository root from test output directory.");
    }

    private sealed record SourceFile(string RelativePath, string Text);

    private sealed record ProjectInspection(
        string RelativePath,
        IReadOnlyList<string> ProjectReferences,
        IReadOnlyList<string> PackageReferences);
}

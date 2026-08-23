using System.Text.RegularExpressions;

namespace MuseumSystem.Web.AcceptanceTests.Documentation;

public sealed class DocumentationScopeBoundaryTests
{
    [Fact]
    public void Feature_002_production_scope_does_not_define_controllers_apis_or_service_hosts()
    {
        var root = RepositoryRoot();
        var productionFiles = DocumentationProductionFiles(root);
        var source = string.Join(Environment.NewLine, productionFiles.Select(File.ReadAllText));
        var projectNames = Directory.GetFiles(Path.Combine(root.FullName, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetFileNameWithoutExtension(path) ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain("[ApiController]", source);
        Assert.DoesNotMatch(@"\b:\s*(ControllerBase|Controller)\b", source);
        Assert.DoesNotContain("@page \"/api", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(projectNames, name => name.Contains("Documentation", StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Service", StringComparison.OrdinalIgnoreCase) || name.Contains("Host", StringComparison.OrdinalIgnoreCase) || name.Contains("Api", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Feature_002_production_scope_has_no_external_document_storage_or_media_management_dependencies()
    {
        var source = string.Join(Environment.NewLine, DocumentationProductionFiles(RepositoryRoot()).Select(File.ReadAllText));

        Assert.DoesNotContain("IFormFile", source);
        Assert.DoesNotContain("InputFile", source);
        Assert.DoesNotContain("BlobContainerClient", source);
        Assert.DoesNotContain("AmazonS3", source);
        Assert.DoesNotContain("Minio", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"\b(Upload|DeleteImage|ReplaceImage|ManageImage|PhotographyUseCase)\b", source);
    }

    [Fact]
    public void Documentation_pages_do_not_offer_out_of_scope_workflows_or_feature_001_ownership_controls()
    {
        var pagesRoot = DocumentationPagesRoot(RepositoryRoot());
        var pageSources = Directory.GetFiles(pagesRoot.FullName, "*.razor")
            .ToDictionary(path => Path.GetFileName(path) ?? path, File.ReadAllText);
        var combined = string.Join(Environment.NewLine, pageSources.Values);

        Assert.DoesNotContain("CreateArtifact", combined);
        Assert.DoesNotContain("SaveArtifact", combined);
        Assert.DoesNotContain("CategoryUseCases.Create", combined);
        Assert.DoesNotContain("LocationUseCases", combined);
        Assert.DoesNotContain("DeliverArtifactsUseCase", combined);
        Assert.DoesNotContain("ReturnArtifactsUseCase", combined);
        Assert.DoesNotContain("MovementRecord.Create", combined);
        Assert.DoesNotContain("PDF", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Word", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Print", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Barcode", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("QRCode", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OCR", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Approval", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Rejection", combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Documentation_application_types_do_not_implement_forbidden_domain_operations()
    {
        var applicationRoot = DocumentationApplicationRoot(RepositoryRoot());
        var typeNames = Directory.GetFiles(applicationRoot.FullName, "*.cs", SearchOption.AllDirectories)
            .SelectMany(ReadDeclaredTypeNames)
            .ToArray();

        Assert.DoesNotContain(typeNames, name => ForbiddenTypeNamePattern().IsMatch(name));
        Assert.Contains("CreateDocumentationRecordUseCase", typeNames);
        Assert.Contains("CorrectCompletedDocumentationUseCase", typeNames);
        Assert.Contains("DocumentationAvailabilityService", typeNames);
    }

    [Fact]
    public void Documentation_remains_a_consumer_of_artifact_and_custody_state()
    {
        var root = RepositoryRoot();
        var createRecord = File.ReadAllText(Path.Combine(DocumentationApplicationRoot(root).FullName, "CreateDocumentationRecordUseCase.cs"));
        var availability = File.ReadAllText(Path.Combine(DocumentationApplicationRoot(root).FullName, "DocumentationAvailabilityService.cs"));
        var domainFiles = string.Join(Environment.NewLine, Directory.GetFiles(Path.Combine(root.FullName, "src", "MuseumSystem.Domain", "Modules", "Documentation"), "*.cs").Select(File.ReadAllText));

        Assert.Contains("dbContext.Artifacts", createRecord);
        Assert.Contains("CurrentStateRules.IsHeldBy", availability);
        Assert.Contains("MovementRecipientType.DocumentationDivision", availability);
        Assert.DoesNotContain("Artifact.Create(", domainFiles);
        Assert.DoesNotContain("DeliverToInternalHolder", domainFiles);
        Assert.DoesNotContain("ReturnToStorage", domainFiles);
    }

    private static Regex ForbiddenTypeNamePattern() =>
        new(
            "(Artifact(Create|Edit|Update)|Category(Admin|Create|Edit|Update)|Location(Admin|Create|Edit|Update)|Custody(Transfer|Mutation)|Movement(Create|Transfer)|Laboratory|Conservation|Exhibition|Loan|Archive|Notification|Approval|Rejection|Export|Print|Barcode|Qr|QRCode|Ocr|AI)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static IEnumerable<string> ReadDeclaredTypeNames(string path)
    {
        var source = File.ReadAllText(path);
        foreach (Match match in Regex.Matches(source, @"\b(?:class|record|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)"))
        {
            yield return match.Groups[1].Value;
        }
    }

    private static IEnumerable<string> DocumentationProductionFiles(DirectoryInfo root) =>
        Directory.GetFiles(Path.Combine(root.FullName, "src", "MuseumSystem.Domain", "Modules", "Documentation"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(root.FullName, "src", "MuseumSystem.Application", "Modules", "Documentation"), "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation"), "*.razor", SearchOption.AllDirectories));

    private static DirectoryInfo DocumentationPagesRoot(DirectoryInfo root) =>
        new(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Documentation"));

    private static DirectoryInfo DocumentationApplicationRoot(DirectoryInfo root) =>
        new(Path.Combine(root.FullName, "src", "MuseumSystem.Application", "Modules", "Documentation"));

    private static DirectoryInfo RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln"))) current = current.Parent;
        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

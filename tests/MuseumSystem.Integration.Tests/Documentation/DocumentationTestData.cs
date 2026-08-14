using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Documentation;

internal static class DocumentationTestData
{
    public static (ArtifactCategory Category, Location Storage, Artifact Artifact, DocumentationTemplate Template, DocumentationTemplateVersion Version) CreateReadyGraph(string prefix)
    {
        var category = ArtifactCategory.Create(prefix, $"Category {prefix}");
        var storage = Location.Create($"Storage {prefix}", LocationType.Storage);
        var artifact = Artifact.Create(category, Random.Shared.Next(1, 1_000_000), $"Artifact {prefix}", storage);
        var template = DocumentationTemplate.Create(category.CategoryId, $"Template {prefix}");
        var version = template.CreateDraftVersion([
            DocumentationTemplateField.Create("title", "Title", DocumentationFieldType.Text, true, 1, "Main"),
            DocumentationTemplateField.Create("condition", "Condition", DocumentationFieldType.SingleSelect, true, 2, "Main", options: [DocumentationTemplateFieldOption.Create("good", "Good", 1)])
        ]);
        template.ActivateVersion(version, "tester");
        return (category, storage, artifact, template, version);
    }

    public static async Task<(Artifact Artifact, DocumentationTemplateVersion Version)> SeedReadyGraphAsync(MuseumDbContext context, string prefix)
    {
        var graph = CreateReadyGraph(prefix);
        context.ArtifactCategories.Add(graph.Category);
        context.Locations.Add(graph.Storage);
        context.Artifacts.Add(graph.Artifact);
        context.DocumentationTemplates.Add(graph.Template);
        await context.SaveChangesAsync();
        return (graph.Artifact, graph.Version);
    }

    public static Dictionary<string, DocumentationFieldValue> CompletedValues(string title = "Documented artifact") => new()
    {
        ["title"] = DocumentationFieldValue.Text(title),
        ["condition"] = DocumentationFieldValue.SingleSelect("good")
    };
}

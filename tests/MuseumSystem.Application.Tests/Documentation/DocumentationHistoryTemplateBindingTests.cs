using System.Reflection;
using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationHistoryTemplateBindingTests
{
    [Fact]
    public async Task Category_change_does_not_rebind_history_or_option_labels()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, version, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var artifact = await db.Artifacts.FindAsync(record.ArtifactId);
        var newCategory = DocumentationApplicationTestHost.AddCategory(db, "NEW");
        DocumentationApplicationTestHost.AddActiveTemplateVersion(db, newCategory, [DocumentationApplicationTestHost.BasicField("new-field")]);
        typeof(Artifact).GetProperty(nameof(Artifact.CategoryId), BindingFlags.Instance | BindingFlags.Public)!.SetValue(artifact, newCategory.CategoryId);
        typeof(Artifact).GetProperty(nameof(Artifact.Category), BindingFlags.Instance | BindingFlags.Public)!.SetValue(artifact, newCategory);
        await db.SaveChangesAsync();

        await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Bound correction"));
        var details = await new GetDocumentationRevisionDetailsUseCase(db, new DocumentationChangeSummaryService())
            .GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 2));

        Assert.True(details.Succeeded);
        Assert.Equal(version.DocumentationTemplateVersionId, details.Value!.TemplateVersion.DocumentationTemplateVersionId);
        Assert.Equal("Condition label", Assert.Single(details.Value.ChangedFields).FieldLabel);
        Assert.Contains("Fair label", details.Value.ChangedFields[0].Summary);
        Assert.DoesNotContain(details.Value.TemplateVersion.Fields, field => field.FieldKey == "new-field");
    }
}

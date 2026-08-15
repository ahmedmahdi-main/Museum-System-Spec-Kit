using System.Reflection;
using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationTemplateBindingUseCaseTests
{
    [Fact]
    public async Task Existing_record_remains_bound_to_original_template_metadata_when_artifact_category_changes_later()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var originalCategory = DocumentationApplicationTestHost.AddCategory(db, "CER");
        var newCategory = DocumentationApplicationTestHost.AddCategory(db, "TXT");
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, originalCategory, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var originalField = DocumentationTemplateField.Create("original", "Original field", DocumentationFieldType.SingleSelect, true, 1, "Main", null,
        [
            DocumentationTemplateFieldOption.Create("legacy", "Legacy", 1)
        ]);
        var originalVersion = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, originalCategory, [originalField]);
        DocumentationApplicationTestHost.AddActiveTemplateVersion(db, newCategory, [DocumentationApplicationTestHost.BasicField("new")]);
        var record = DocumentationRecord.Create(artifact.ArtifactId, originalVersion, "tester");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        SetCategory(artifact, newCategory);
        await db.SaveChangesAsync();

        var result = await new GetDocumentationRecordForEditUseCase(db, new DocumentationAvailabilityService())
            .GetDocumentationRecordForEdit(new GetDocumentationRecordForEditRequest(record.DocumentationRecordId, new DocumentationActionPermissionSet(CanEdit: true)));

        Assert.True(result.Succeeded);
        Assert.Equal(newCategory.CategoryId, result.Value!.Artifact.CategoryId);
        Assert.Equal(newCategory.CategoryCode, result.Value.Artifact.CategoryCode);
        Assert.Equal(newCategory.NameArabic, result.Value.Artifact.CategoryName);
        Assert.Equal(originalVersion.DocumentationTemplateVersionId, result.Value.Record.DocumentationTemplateVersionId);
        Assert.Equal(originalCategory.CategoryId, result.Value.TemplateVersion.ArtifactCategoryId);
        Assert.Equal(originalCategory.CategoryCode, result.Value.TemplateVersion.ArtifactCategoryCode);
        Assert.Equal(originalCategory.NameArabic, result.Value.TemplateVersion.ArtifactCategoryName);
        Assert.Contains(result.Value.TemplateVersion.Fields, field => field.FieldKey == "original" && field.Options.Any(option => option.OptionKey == "legacy"));
        Assert.DoesNotContain(result.Value.TemplateVersion.Fields, field => field.FieldKey == "new");
    }

    private static void SetCategory(Artifact artifact, ArtifactCategory category)
    {
        typeof(Artifact).GetProperty(nameof(Artifact.CategoryId), BindingFlags.Instance | BindingFlags.Public)!.SetValue(artifact, category.CategoryId);
        typeof(Artifact).GetProperty(nameof(Artifact.Category), BindingFlags.Instance | BindingFlags.Public)!.SetValue(artifact, category);
    }
}

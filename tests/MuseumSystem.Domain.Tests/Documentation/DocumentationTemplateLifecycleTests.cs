using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Domain.Tests.Documentation;

public sealed class DocumentationTemplateLifecycleTests
{
    [Fact]
    public void Activating_version_retires_previous_active_version()
    {
        var template = DocumentationTemplate.Create(Guid.NewGuid(), "Ceramics", actor: "creator");
        var v1 = template.CreateDraftVersion([TestField("material")], "author");
        var v2 = template.CreateDraftVersion([TestField("shape")], "author");

        template.ActivateVersion(v1, "manager");
        template.ActivateVersion(v2, "manager");

        Assert.Equal(DocumentationTemplateVersionStatus.Retired, v1.Status);
        Assert.Equal(DocumentationTemplateVersionStatus.Active, v2.Status);
        Assert.Single(template.Versions, v => v.Status == DocumentationTemplateVersionStatus.Active);
        Assert.Equal("creator", template.CreatedBy);
        Assert.Equal("manager", template.LastModifiedBy);
        Assert.Equal("author", v1.CreatedBy);
    }

    [Fact]
    public void Active_version_can_be_retired_without_replacement()
    {
        var template = DocumentationTemplate.Create(Guid.NewGuid(), "Ceramics");
        var version = template.CreateDraftVersion([TestField("material")]);

        template.ActivateVersion(version);
        template.RetireVersion(version);

        Assert.Equal(DocumentationTemplateVersionStatus.Retired, version.Status);
        Assert.DoesNotContain(template.Versions, v => v.Status == DocumentationTemplateVersionStatus.Active);
    }

    [Fact]
    public void Draft_version_cannot_be_retired()
    {
        var template = DocumentationTemplate.Create(Guid.NewGuid(), "Ceramics");
        var version = template.CreateDraftVersion([TestField("material")]);

        Assert.Throws<InvalidOperationException>(() => template.RetireVersion(version));
        Assert.Equal(DocumentationTemplateVersionStatus.Draft, version.Status);
    }

    [Fact]
    public void Draft_select_fields_may_be_empty_but_activation_requires_options()
    {
        var template = DocumentationTemplate.Create(Guid.NewGuid(), "Ceramics");
        var version = template.CreateDraftVersion([
            DocumentationTemplateField.Create("condition", "Condition", DocumentationFieldType.SingleSelect, true, 1, "Main")
        ]);

        Assert.Throws<InvalidOperationException>(() => template.ActivateVersion(version));

        version.ReplaceFields([
            DocumentationTemplateField.Create("condition", "Condition", DocumentationFieldType.SingleSelect, true, 1, "Main", options: [DocumentationTemplateFieldOption.Create("good", "Good", 1)])
        ]);
        template.ActivateVersion(version);

        Assert.Equal(DocumentationTemplateVersionStatus.Active, version.Status);
    }

    private static DocumentationTemplateField TestField(string key) => DocumentationTemplateField.Create(key, key, DocumentationFieldType.Text, true, 1, "Main");
}

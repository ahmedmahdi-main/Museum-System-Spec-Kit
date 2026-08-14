using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Domain.Tests.Documentation;

public sealed class UsedTemplateVersionImmutabilityTests
{
    [Fact]
    public void Used_template_version_cannot_change_field_definitions_but_can_retire()
    {
        var version = DocumentationTemplateVersion.CreateDraft(1, [DocumentationTemplateField.Create("name", "Name", DocumentationFieldType.Text, true, 1, "Main")]);
        version.Activate();
        version.MarkUsed();

        Assert.Throws<InvalidOperationException>(() => version.ReplaceFields([DocumentationTemplateField.Create("other", "Other", DocumentationFieldType.Text, true, 1, "Main")]));

        version.Retire("manager");
        Assert.Equal(DocumentationTemplateVersionStatus.Retired, version.Status);
    }
}

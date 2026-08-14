using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Domain.Tests.Documentation;

public sealed class DocumentationTemplateVersionTests
{
    [Fact]
    public void Template_field_keys_must_be_unique_inside_version()
    {
        var fields = new[]
        {
            DocumentationTemplateField.Create("name", "Name", DocumentationFieldType.Text, true, 1, "Main"),
            DocumentationTemplateField.Create("name", "Duplicate", DocumentationFieldType.Text, false, 2, "Main")
        };

        Assert.Throws<InvalidOperationException>(() => DocumentationTemplateVersion.CreateDraft(1, fields));
    }

    [Fact]
    public void Select_option_keys_must_be_unique_inside_field()
    {
        Assert.Throws<InvalidOperationException>(() => DocumentationTemplateField.Create(
            "condition",
            "Condition",
            DocumentationFieldType.SingleSelect,
            true,
            1,
            "Main",
            options:
            [
                DocumentationTemplateFieldOption.Create("good", "Good", 1),
                DocumentationTemplateFieldOption.Create("good", "Also good", 2)
            ]));
    }
}

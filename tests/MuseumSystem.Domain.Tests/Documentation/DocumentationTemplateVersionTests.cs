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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Field_display_order_must_be_positive(int displayOrder)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DocumentationTemplateField.Create(
            "condition",
            "Condition",
            DocumentationFieldType.Text,
            true,
            displayOrder,
            "Main"));

        Assert.Contains("display order", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Option_display_order_must_be_positive(int displayOrder)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DocumentationTemplateField.Create(
            "condition",
            "Condition",
            DocumentationFieldType.SingleSelect,
            true,
            1,
            "Main",
            options: [DocumentationTemplateFieldOption.Create("good", "Good", displayOrder)]));

        Assert.Contains("Option display order", exception.Message);
    }

    [Fact]
    public void Positive_display_orders_are_preserved()
    {
        var field = DocumentationTemplateField.Create(
            "condition",
            "Condition",
            DocumentationFieldType.SingleSelect,
            true,
            3,
            "Main",
            options: [DocumentationTemplateFieldOption.Create("good", "Good", 2)]);

        Assert.Equal(3, field.DisplayOrder);
        Assert.Equal(2, field.Options.Single().DisplayOrder);
    }
}
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Domain.Tests.Documentation;

public sealed class DocumentationFieldTypeTests
{
    [Fact]
    public void Supported_field_types_match_feature_scope()
    {
        var names = Enum.GetNames<DocumentationFieldType>();

        Assert.Equal([
            "Text",
            "MultilineText",
            "Number",
            "Date",
            "Boolean",
            "SingleSelect",
            "MultiSelect"
        ], names);
    }
}

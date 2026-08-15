using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation.Contracts;

public sealed record DocumentationFieldValueDto(
    string FieldKey,
    DocumentationFieldType FieldType,
    string? TextValue,
    decimal? NumberValue,
    DateOnly? DateValue,
    bool? BooleanValue,
    string? OptionKey,
    IReadOnlyList<string> OptionKeys);

public sealed class DocumentationFieldValueInputDto
{
    public string FieldKey { get; set; } = string.Empty;
    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public DateOnly? DateValue { get; set; }
    public bool? BooleanValue { get; set; }
    public string? OptionKey { get; set; }
    public List<string> OptionKeys { get; set; } = [];
}

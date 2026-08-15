using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation.Contracts;

public sealed record DocumentationTemplateFieldDto(
    Guid DocumentationTemplateFieldId,
    string FieldKey,
    string Label,
    DocumentationFieldType FieldType,
    bool IsRequired,
    int DisplayOrder,
    string Section,
    string? HelpText,
    IReadOnlyList<DocumentationTemplateFieldOptionDto> Options);

public sealed record DocumentationTemplateFieldOptionDto(
    Guid DocumentationTemplateFieldOptionId,
    string OptionKey,
    string Label,
    int DisplayOrder);

public sealed record DocumentationTemplateFieldInputDto(
    string FieldKey,
    string Label,
    DocumentationFieldType FieldType,
    bool IsRequired,
    int DisplayOrder,
    string Section,
    string? HelpText,
    IReadOnlyList<DocumentationTemplateFieldOptionInputDto> Options);

public sealed record DocumentationTemplateFieldOptionInputDto(
    string OptionKey,
    string Label,
    int DisplayOrder);

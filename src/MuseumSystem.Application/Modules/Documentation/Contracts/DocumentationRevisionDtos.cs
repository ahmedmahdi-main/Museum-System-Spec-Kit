namespace MuseumSystem.Application.Modules.Documentation.Contracts;

public sealed record CorrectCompletedDocumentationRequest(
    Guid DocumentationRecordId,
    int ExpectedConcurrencyToken,
    IReadOnlyList<DocumentationFieldValueInputDto> Values,
    string Reason);

public sealed record CorrectCompletedDocumentationResultDto(
    DocumentationRecordSummaryDto Record,
    int RevisionNumber,
    IReadOnlyList<DocumentationFieldValueDto> CurrentValues);

public sealed record GetDocumentationHistoryRequest(Guid DocumentationRecordId);

public sealed record GetDocumentationRevisionDetailsRequest(
    Guid DocumentationRecordId,
    int RevisionNumber);

public sealed record DocumentationHistoryItemDto(
    Guid DocumentationRecordId,
    int RevisionNumber,
    bool IsCompletionBaseline,
    string? Reason,
    string? Author,
    DateTimeOffset Timestamp,
    IReadOnlyList<DocumentationFieldChangeDto> ChangedFields);

public sealed record DocumentationRevisionDetailsDto(
    Guid DocumentationRecordId,
    int RevisionNumber,
    bool IsCompletionBaseline,
    DocumentationTemplateVersionDetailsDto TemplateVersion,
    IReadOnlyList<DocumentationFieldValueDto> BaselineValues,
    IReadOnlyList<DocumentationFieldValueDto> PreviousValues,
    IReadOnlyList<DocumentationFieldValueDto> NewValues,
    IReadOnlyList<DocumentationFieldChangeDto> ChangedFields,
    string? Reason,
    string? Author,
    DateTimeOffset Timestamp);

public sealed record DocumentationFieldChangeDto(
    string FieldKey,
    string FieldLabel,
    DocumentationFieldValueDto? PreviousValue,
    DocumentationFieldValueDto? NewValue,
    string Summary);

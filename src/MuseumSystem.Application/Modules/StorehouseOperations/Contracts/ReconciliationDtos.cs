using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations.Contracts;

public sealed record StartReconciliationSessionRequest(Guid LocationId, string? Note = null);

public sealed record RecordReconciliationItemsRequest(Guid ReconciliationSessionId, IReadOnlyList<string> ObservedMuseumNumbers);

public sealed record CreateDocumentedCorrectionRequest(
    Guid ReconciliationResultId,
    DocumentedCorrectionType CorrectionType,
    Guid? NewLocationId,
    MovementRecipientType? NewHolderType,
    string? NewHolderName,
    string Reason);

public sealed record ReconciliationSessionDto(
    Guid ReconciliationSessionId,
    Guid LocationId,
    string? LocationName,
    ReconciliationSessionStatus Status,
    string? Note,
    IReadOnlyList<ReconciliationResultDto> Results);

public sealed record ReconciliationResultDto(
    Guid ReconciliationResultId,
    Guid? ArtifactId,
    string? ObservedMuseumNumber,
    Guid? ExpectedLocationId,
    Guid? ObservedLocationId,
    ReconciliationResultType ResultType,
    string IssueDescription,
    bool IsConfirmed);

public sealed record DocumentedCorrectionDto(
    Guid CorrectionId,
    Guid ArtifactId,
    DocumentedCorrectionType CorrectionType,
    string PreviousValueSummary,
    string NewValueSummary,
    string Reason,
    DateTimeOffset CorrectedAt);

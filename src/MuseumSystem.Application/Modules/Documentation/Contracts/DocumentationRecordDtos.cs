using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation.Contracts;

public enum DocumentationArtifactDocumentationStatus
{
    None = 0,
    Draft = 1,
    Completed = 2
}

public sealed record DocumentationActionPermissionSet(
    bool CanCreate = false,
    bool CanEdit = false,
    bool CanComplete = false);

public sealed record DocumentationActionAvailabilityDto(
    bool CanCreate,
    string? CreateBlockedReason,
    bool CanResumeDraft,
    bool CanSaveDraft,
    string? DraftEditBlockedReason,
    bool CanComplete,
    string? CompleteBlockedReason,
    bool CanViewCompleted);

public sealed record DocumentationArtifactSummaryDto(
    Guid ArtifactId,
    Guid CategoryId,
    string CategoryCode,
    string CategoryName,
    string MuseumNumber,
    string BasicDescription,
    ArtifactCurrentStatus CurrentStatus,
    Guid? CurrentLocationId,
    string? CurrentLocationName,
    string? CurrentHolderType,
    string? CurrentHolderName,
    Guid? LastKnownStorageLocationId,
    bool IsAvailableToDocumentation,
    string? DocumentationAvailabilityReason);

public sealed record DocumentationRecordSummaryDto(
    Guid DocumentationRecordId,
    Guid ArtifactId,
    Guid DocumentationTemplateVersionId,
    int TemplateVersionNumber,
    DocumentationRecordStatus Status,
    int ConcurrencyToken,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? LastModifiedAt,
    string? LastModifiedBy,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    bool HasRevision1Baseline);

public sealed record DocumentationWorkspaceDto(
    DocumentationArtifactSummaryDto Artifact,
    DocumentationArtifactDocumentationStatus DocumentationStatus,
    DocumentationRecordSummaryDto? ExistingRecord,
    DocumentationTemplateVersionDetailsDto? ActiveTemplateVersion,
    DocumentationActionAvailabilityDto Actions);

public sealed record DocumentationRecordEditDto(
    DocumentationArtifactSummaryDto Artifact,
    DocumentationRecordSummaryDto Record,
    DocumentationTemplateVersionDetailsDto TemplateVersion,
    IReadOnlyList<DocumentationFieldValueDto> Values,
    DocumentationActionAvailabilityDto Actions);

public sealed record SearchDocumentationArtifactRequest(
    string MuseumNumber,
    DocumentationActionPermissionSet Permissions);

public sealed record GetDocumentationWorkspaceRequest(
    Guid ArtifactId,
    DocumentationActionPermissionSet Permissions);

public sealed record CreateDocumentationRecordRequest(Guid ArtifactId);

public sealed record GetDocumentationRecordForEditRequest(
    Guid DocumentationRecordId,
    DocumentationActionPermissionSet Permissions);

public sealed record SaveDocumentationDraftRequest(
    Guid DocumentationRecordId,
    int ExpectedConcurrencyToken,
    IReadOnlyList<DocumentationFieldValueInputDto> Values);

public sealed record CompleteDocumentationRecordRequest(
    Guid DocumentationRecordId,
    int ExpectedConcurrencyToken,
    IReadOnlyList<DocumentationFieldValueInputDto> Values);

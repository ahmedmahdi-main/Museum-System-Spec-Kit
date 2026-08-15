using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation.Contracts;

public sealed record DocumentationTemplateListItemDto(
    Guid DocumentationTemplateId,
    Guid ArtifactCategoryId,
    string ArtifactCategoryCode,
    string ArtifactCategoryName,
    string Name,
    string? Description,
    int VersionCount,
    int DraftVersionCount,
    int ActiveVersionCount,
    int RetiredVersionCount,
    DocumentationTemplateVersionSummaryDto? LatestVersion,
    DocumentationTemplateVersionSummaryDto? ActiveVersion,
    IReadOnlyList<DocumentationTemplateVersionSummaryDto> Versions);

public sealed record DocumentationTemplateVersionSummaryDto(
    Guid DocumentationTemplateVersionId,
    int VersionNumber,
    DocumentationTemplateVersionStatus Status,
    bool IsUsed,
    int ConcurrencyToken,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? RetiredAt);

public sealed record DocumentationTemplateVersionDetailsDto(
    Guid DocumentationTemplateId,
    Guid DocumentationTemplateVersionId,
    Guid ArtifactCategoryId,
    string ArtifactCategoryCode,
    string ArtifactCategoryName,
    string TemplateName,
    string? TemplateDescription,
    int VersionNumber,
    DocumentationTemplateVersionStatus Status,
    bool IsUsed,
    bool IsReadOnly,
    int ConcurrencyToken,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? LastModifiedAt,
    string? LastModifiedBy,
    DateTimeOffset? ActivatedAt,
    string? ActivatedBy,
    DateTimeOffset? RetiredAt,
    string? RetiredBy,
    IReadOnlyList<DocumentationTemplateFieldDto> Fields);

public sealed record CreateDocumentationTemplateRequest(
    Guid ArtifactCategoryId,
    string Name,
    string? Description);

public sealed record CreateTemplateVersionDraftRequest(
    Guid DocumentationTemplateId,
    Guid? SourceTemplateVersionId = null);

public sealed record SaveTemplateVersionDraftRequest(
    Guid DocumentationTemplateVersionId,
    int ExpectedConcurrencyToken,
    IReadOnlyList<DocumentationTemplateFieldInputDto> Fields);

public sealed record ActivateTemplateVersionRequest(
    Guid DocumentationTemplateVersionId,
    int ExpectedConcurrencyToken);

public sealed record RetireTemplateVersionRequest(
    Guid DocumentationTemplateVersionId,
    int ExpectedConcurrencyToken);

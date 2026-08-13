using MuseumSystem.Domain.Modules.Import;

namespace MuseumSystem.Application.Modules.Import.Contracts;

public sealed record UploadImportFileForPreviewRequest(string FileName, Stream Content);

public sealed record ImportBatchDto(
    Guid ImportBatchId,
    string FileName,
    ImportBatchStatus Status,
    int TotalRows,
    int AcceptedRows,
    int RejectedRows,
    IReadOnlyList<ImportRowDto> Rows);

public sealed record ImportRowDto(
    Guid ImportRowId,
    int RowNumber,
    string? CategoryValue,
    string? ItemNumberValue,
    string? LocationValue,
    string? DescriptionValue,
    ImportRowStatus Status,
    string Issues,
    Guid? ProposedCategoryId,
    Guid? ProposedLocationId,
    Guid? ProposedArtifactId);

public sealed record ImportCommitDto(Guid ImportBatchId, int CreatedArtifacts, string Message);

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Modules.Documentation;

public sealed class GetDocumentationHistoryUseCase(
    IMuseumDbContext dbContext,
    DocumentationChangeSummaryService changeSummaryService)
{
    public async Task<UseCaseResult<IReadOnlyList<DocumentationHistoryItemDto>>> GetDocumentationHistory(
        GetDocumentationHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var record = await dbContext.DocumentationRecords.AsNoTracking()
            .Include(item => item.DocumentationTemplateVersion)!.ThenInclude(version => version!.Fields).ThenInclude(field => field.Options)
            .Include(item => item.Revisions)
            .FirstOrDefaultAsync(item => item.DocumentationRecordId == request.DocumentationRecordId, cancellationToken);

        if (record?.DocumentationTemplateVersion is null)
            return Failure("DocumentationRecord.NotFound", "Documentation Record was not found.");
        if (record.Status != DocumentationRecordStatus.Completed)
            return Failure("DocumentationRecord.NotCompleted", "History is available only for Completed documentation records.");
        if (string.IsNullOrWhiteSpace(record.CompletedBaselineValuesJson) || record.CompletedAt is null)
            return Failure("DocumentationRecord.BaselineMissing", "The Revision 1 completion baseline is missing.");

        var revisions = record.Revisions.OrderBy(item => item.RevisionNumber).ToList();
        if (revisions.Any(item => item.RevisionNumber < 2 || string.IsNullOrWhiteSpace(item.Reason)) ||
            revisions.Select(item => item.RevisionNumber).Distinct().Count() != revisions.Count)
            return Failure("DocumentationRecord.HistoryInvalid", "Documentation revision history is invalid.");

        var items = new List<DocumentationHistoryItemDto>
        {
            new(record.DocumentationRecordId, 1, true, null, record.CompletedBy, record.CompletedAt.Value, [])
        };
        items.AddRange(revisions.Select(revision => new DocumentationHistoryItemDto(
            record.DocumentationRecordId,
            revision.RevisionNumber,
            false,
            revision.Reason,
            revision.CreatedBy,
            revision.CreatedAt,
            changeSummaryService.Deserialize(revision.ChangeSummaryJson))));
        return UseCaseResult<IReadOnlyList<DocumentationHistoryItemDto>>.Success(items);
    }

    private static UseCaseResult<IReadOnlyList<DocumentationHistoryItemDto>> Failure(string code, string message) =>
        UseCaseResult<IReadOnlyList<DocumentationHistoryItemDto>>.Failure(new ValidationIssue(code, message));
}

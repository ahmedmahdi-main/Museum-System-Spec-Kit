using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class MovementHistoryUseCase(IMuseumDbContext dbContext)
{
    public async Task<IReadOnlyList<MovementHistoryDto>> GetMovementHistory(Guid artifactId, CancellationToken cancellationToken = default) =>
        await dbContext.MovementRecords
            .Include(record => record.Artifact)
            .Include(record => record.ReturnLocation)
            .AsNoTracking()
            .Where(record => record.ArtifactId == artifactId)
            .OrderByDescending(record => record.OccurredAt)
            .Select(record => new MovementHistoryDto(
                record.MovementId,
                record.MovementType,
                record.MovementGroupId,
                record.Artifact != null ? record.Artifact.MuseumNumberDisplay : string.Empty,
                record.RecipientType,
                record.RecipientName,
                record.Purpose,
                record.ReturnLocationId,
                record.ReturnLocation != null ? record.ReturnLocation.NameArabic : null,
                record.Note,
                record.OccurredAt,
                record.RecordedBy))
            .ToListAsync(cancellationToken);
}

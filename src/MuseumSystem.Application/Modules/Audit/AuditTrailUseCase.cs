using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.IdentityAccess;

namespace MuseumSystem.Application.Modules.Audit;

public sealed record AuditEntryDto(Guid AuditEntryId, string? ActorUserId, string ActionName, string ModuleName, string EntityName, string EntityId, DateTimeOffset OccurredAt, string Summary, string? ChangeSummary);

public sealed class AuditTrailUseCase(IMuseumDbContext dbContext)
{
    public async Task<IReadOnlyList<AuditEntryDto>> ListAuditEntries(CancellationToken cancellationToken = default) =>
        await dbContext.AuditEntries
            .AsNoTracking()
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(200)
            .Select(entry => new AuditEntryDto(entry.AuditEntryId, entry.ActorUserId, entry.ActionName, entry.ModuleName, entry.EntityName, entry.EntityId, entry.OccurredAt, entry.Summary, entry.ChangeSummary))
            .ToListAsync(cancellationToken);
}

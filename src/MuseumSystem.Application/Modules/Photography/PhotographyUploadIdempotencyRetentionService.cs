using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

/// <summary>
/// Internal/system service that purges expired, fully-resolved <see cref="PhotographyUploadOperation"/>
/// idempotency records. Not staff-facing: no permission check, no endpoint, no scheduler. Callers decide
/// when to invoke <see cref="CleanupExpiredAsync"/>; this service performs one cleanup pass only.
/// </summary>
public sealed class PhotographyUploadIdempotencyRetentionService(
    IMuseumDbContext dbContext,
    TimeProvider clock,
    IOptions<PhotographyIdempotencyOptions> options)
{
    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - TimeSpan.FromDays(options.Value.RetentionDays);

        var candidates = await dbContext.PhotographyUploadOperations
            .Include(operation => operation.FileOutcomes)
            .Where(operation =>
                operation.LastSeenAt <= cutoff
                && (operation.Status == PhotographyUploadOperationStatus.Completed
                    || operation.Status == PhotographyUploadOperationStatus.CompletedWithFailures
                    || operation.Status == PhotographyUploadOperationStatus.Failed))
            .ToListAsync(cancellationToken);

        var removed = 0;
        foreach (var operation in candidates)
        {
            if (operation.FileOutcomes.Any(outcome => outcome.IsUnresolved))
            {
                continue;
            }

            var hasUnresolvedLinkedRecovery = await dbContext.StorageOperationRecoveries.AnyAsync(
                recovery =>
                    recovery.PhotographyUploadOperationId == operation.PhotographyUploadOperationId
                    && recovery.Status != StorageOperationRecoveryStatus.Resolved,
                cancellationToken);

            if (hasUnresolvedLinkedRecovery)
            {
                continue;
            }

            if (await TryDeleteOperationAsync(operation, cancellationToken))
            {
                removed++;
            }
        }

        return removed;
    }

    private async Task<bool> TryDeleteOperationAsync(PhotographyUploadOperation operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.PhotographyUploadFileOutcomes.RemoveRange(operation.FileOutcomes);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.PhotographyUploadOperations.Remove(operation);
            await dbContext.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return false;
        }
    }
}

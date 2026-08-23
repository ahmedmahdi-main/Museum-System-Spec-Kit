using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;

namespace MuseumSystem.Application.Modules.Documentation;

public static class DocumentationConcurrencyHandler
{
    public static UseCaseResult<T> StaleRequest<T>(string message) =>
        UseCaseResult<T>.Conflict(WithReloadReviewGuidance(message));

    public static UseCaseResult<T> OptimisticWriteConflict<T>(
        IMuseumDbContext dbContext,
        DbUpdateConcurrencyException exception,
        string message)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(exception);
        dbContext.ClearTrackedChanges();
        return StaleRequest<T>(message);
    }

    public static UseCaseResult<T> CompetingWriteConflict<T>(
        IMuseumDbContext dbContext,
        string message)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        dbContext.ClearTrackedChanges();
        return StaleRequest<T>(message);
    }

    private static string WithReloadReviewGuidance(string message)
    {
        if (message.Contains("reload", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("review", StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        return $"{message.Trim().TrimEnd('.')} Reload and review the latest state before trying again.";
    }
}

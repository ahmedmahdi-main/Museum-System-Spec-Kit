using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public static class DocumentedCorrectionRules
{
    public static void EnsureCanCreateFromConflict(ReconciliationResult? result, string? reason)
    {
        if (result is null || result.ResultType != ReconciliationResultType.Conflict || !result.IsConfirmed)
        {
            throw new InvalidOperationException("Documented correction requires a confirmed reconciliation conflict.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A documented reason is required.", nameof(reason));
        }
    }

    public static bool WouldSubstituteReturn(Artifact artifact, Location? newLocation) =>
        artifact.CurrentStatus == ArtifactCurrentStatus.OutOfStorage && newLocation?.LocationType == LocationType.Storage;
}

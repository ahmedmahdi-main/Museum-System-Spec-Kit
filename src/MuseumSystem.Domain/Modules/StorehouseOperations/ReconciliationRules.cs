using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public static class ReconciliationRules
{
    public static IReadOnlyList<ReconciliationResult> Classify(Guid sessionId, Guid locationId, IReadOnlyList<Artifact> artifacts, IReadOnlyList<string> observedMuseumNumbers)
    {
        var observed = observedMuseumNumbers
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToList();
        var observedCounts = observed.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var byMuseumNumber = artifacts.ToDictionary(artifact => artifact.MuseumNumberDisplay, StringComparer.OrdinalIgnoreCase);
        var results = new List<ReconciliationResult>();

        foreach (var artifact in artifacts.Where(a => a.CurrentLocationId == locationId && a.CurrentStatus == ArtifactCurrentStatus.InStorage))
        {
            if (observedCounts.ContainsKey(artifact.MuseumNumberDisplay))
            {
                results.Add(ReconciliationResult.Create(sessionId, artifact.ArtifactId, artifact.MuseumNumberDisplay, locationId, locationId, ReconciliationResultType.Matched, "القطعة مطابقة."));
            }
            else
            {
                results.Add(ReconciliationResult.Create(sessionId, artifact.ArtifactId, artifact.MuseumNumberDisplay, locationId, null, ReconciliationResultType.Missing, "القطعة متوقعة في الموقع ولم تظهر في الجرد."));
            }
        }

        foreach (var observedNumber in observed.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (observedCounts[observedNumber] > 1)
            {
                var duplicateArtifact = byMuseumNumber.GetValueOrDefault(observedNumber);
                results.Add(ReconciliationResult.Create(sessionId, duplicateArtifact?.ArtifactId, observedNumber, duplicateArtifact?.CurrentLocationId, locationId, ReconciliationResultType.NeedsReview, "رقم ملاحظ مكرر في الجرد."));
                continue;
            }

            if (!byMuseumNumber.TryGetValue(observedNumber, out var artifact))
            {
                results.Add(ReconciliationResult.Create(sessionId, null, observedNumber, null, locationId, ReconciliationResultType.Extra, "رقم غير موجود في السجل المركزي."));
                continue;
            }

            if (artifact.CurrentLocationId != locationId || artifact.CurrentStatus != ArtifactCurrentStatus.InStorage)
            {
                results.Add(ReconciliationResult.Create(sessionId, artifact.ArtifactId, observedNumber, artifact.CurrentLocationId, locationId, ReconciliationResultType.Conflict, "القطعة موجودة في موقع يخالف حالتها الحالية."));
            }
        }

        return results;
    }
}

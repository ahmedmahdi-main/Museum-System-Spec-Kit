using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Tests.StorehouseOperations;

public sealed class ReconciliationClassificationTests
{
    [Fact]
    public void Classification_returns_matched_missing_extra_conflict_and_needs_review()
    {
        var category = ArtifactCategory.Create("ARC", "Archive");
        var locationA = Location.Create("Shelf A", LocationType.Storage);
        var locationB = Location.Create("Shelf B", LocationType.Storage);
        var matched = Artifact.Create(category, 1, "Matched", locationA);
        var missing = Artifact.Create(category, 2, "Missing", locationA);
        var conflict = Artifact.Create(category, 3, "Conflict", locationB);
        var duplicate = Artifact.Create(category, 4, "Duplicate", locationA);

        var results = ReconciliationRules.Classify(
            Guid.NewGuid(),
            locationA.LocationId,
            [matched, missing, conflict, duplicate],
            [matched.MuseumNumberDisplay, conflict.MuseumNumberDisplay, duplicate.MuseumNumberDisplay, duplicate.MuseumNumberDisplay, "UNKNOWN-1"]);

        Assert.Contains(results, r => r.ResultType == ReconciliationResultType.Matched && r.ArtifactId == matched.ArtifactId);
        Assert.Contains(results, r => r.ResultType == ReconciliationResultType.Missing && r.ArtifactId == missing.ArtifactId);
        Assert.Contains(results, r => r.ResultType == ReconciliationResultType.Extra && r.ObservedMuseumNumber == "UNKNOWN-1");
        Assert.Contains(results, r => r.ResultType == ReconciliationResultType.Conflict && r.ArtifactId == conflict.ArtifactId);
        Assert.Contains(results, r => r.ResultType == ReconciliationResultType.NeedsReview && r.ArtifactId == duplicate.ArtifactId);
    }

    [Fact]
    public void Conflict_result_does_not_change_artifact_state()
    {
        var category = ArtifactCategory.Create("ARC", "Archive");
        var locationA = Location.Create("Shelf A", LocationType.Storage);
        var locationB = Location.Create("Shelf B", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Conflict", locationB);

        _ = ReconciliationRules.Classify(Guid.NewGuid(), locationA.LocationId, [artifact], [artifact.MuseumNumberDisplay]);

        Assert.Equal(locationB.LocationId, artifact.CurrentLocationId);
        Assert.Equal(locationB.LocationId, artifact.LastKnownStorageLocationId);
        Assert.Null(artifact.CurrentHolderName);
    }
}

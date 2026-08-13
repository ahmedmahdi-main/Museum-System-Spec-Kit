using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Tests.StorehouseOperations;

public sealed class DocumentedCorrectionTests
{
    [Fact]
    public void Correction_requires_confirmed_conflict_and_reason()
    {
        var result = ReconciliationResult.Create(Guid.NewGuid(), Guid.NewGuid(), "ARC-1", null, Guid.NewGuid(), ReconciliationResultType.Conflict, "Conflict");

        Assert.Throws<InvalidOperationException>(() => DocumentedCorrectionRules.EnsureCanCreateFromConflict(result, "Confirmed reason"));

        result.ConfirmConflict();
        Assert.Throws<ArgumentException>(() => DocumentedCorrectionRules.EnsureCanCreateFromConflict(result, " "));
        DocumentedCorrectionRules.EnsureCanCreateFromConflict(result, "Confirmed reason");
    }

    [Fact]
    public void Storage_correction_is_not_return_substitute_for_out_of_storage_artifact()
    {
        var category = ArtifactCategory.Create("ARC", "Archive");
        var storage = Location.Create("Shelf A", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, "Artifact", storage);
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Lab");

        Assert.True(DocumentedCorrectionRules.WouldSubstituteReturn(artifact, storage));
        Assert.Throws<InvalidOperationException>(() => artifact.CorrectStorageLocation(storage));
    }

    [Fact]
    public void Documented_correction_records_previous_and_new_summaries_without_movement_history()
    {
        var correction = DocumentedCorrection.Create(Guid.NewGuid(), DocumentedCorrectionSourceType.Reconciliation, Guid.NewGuid(), DocumentedCorrectionType.LocationCorrection, "before", "after", "Confirmed by inventory");

        Assert.Equal("before", correction.PreviousValueSummary);
        Assert.Equal("after", correction.NewValueSummary);
        Assert.Equal("Confirmed by inventory", correction.Reason);
    }
}

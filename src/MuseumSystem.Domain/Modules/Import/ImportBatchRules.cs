namespace MuseumSystem.Domain.Modules.Import;

public static class ImportBatchRules
{
    public static bool CanValidate(ImportBatch batch) => batch.Status is ImportBatchStatus.Previewed or ImportBatchStatus.ValidatedWithErrors or ImportBatchStatus.ReadyToCommit;

    public static bool CanCommit(ImportBatch batch) => batch.Status == ImportBatchStatus.ReadyToCommit;

    public static bool CanCancel(ImportBatch batch) => batch.Status is ImportBatchStatus.Previewed or ImportBatchStatus.ValidatedWithErrors or ImportBatchStatus.ReadyToCommit;
}

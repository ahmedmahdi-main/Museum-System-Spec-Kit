namespace MuseumSystem.Domain.Modules.Import;

public enum ImportBatchStatus
{
    Previewed = 1,
    ValidatedWithErrors = 2,
    ReadyToCommit = 3,
    Committed = 4,
    Cancelled = 5
}

public sealed class ImportBatch
{
    private readonly List<ImportRow> _rows = [];

    private ImportBatch()
    {
    }

    private ImportBatch(string fileName)
    {
        ImportBatchId = Guid.NewGuid();
        FileName = RequireText(fileName, nameof(fileName));
        Status = ImportBatchStatus.Previewed;
        UploadedAt = DateTimeOffset.UtcNow;
    }

    public Guid ImportBatchId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public ImportBatchStatus Status { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public string? UploadedBy { get; private set; }
    public DateTimeOffset? ValidatedAt { get; private set; }
    public string? ValidatedBy { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }
    public string? CommittedBy { get; private set; }
    public int TotalRows { get; private set; }
    public int AcceptedRows { get; private set; }
    public int RejectedRows { get; private set; }
    public int ConcurrencyToken { get; private set; }
    public IReadOnlyCollection<ImportRow> Rows => _rows.AsReadOnly();

    public static ImportBatch Create(string fileName) => new(fileName);

    public void AddRow(ImportRow row)
    {
        EnsurePreviewEditable();
        _rows.Add(row);
        TotalRows = _rows.Count;
        Touch();
    }

    public void MarkValidated(string? validatedBy = null)
    {
        EnsureNotFinal();
        TotalRows = _rows.Count;
        AcceptedRows = _rows.Count(row => row.Status == ImportRowStatus.Accepted);
        RejectedRows = _rows.Count(row => row.Status == ImportRowStatus.Rejected);
        var reviewRows = _rows.Count(row => row.Status == ImportRowStatus.NeedsReview);
        Status = RejectedRows == 0 && reviewRows == 0 && AcceptedRows > 0
            ? ImportBatchStatus.ReadyToCommit
            : ImportBatchStatus.ValidatedWithErrors;
        ValidatedAt = DateTimeOffset.UtcNow;
        ValidatedBy = NormalizeOptional(validatedBy);
        Touch();
    }

    public void MarkCommitted(string? committedBy = null)
    {
        if (Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException("Import batch is already committed.");
        }

        if (Status != ImportBatchStatus.ReadyToCommit)
        {
            throw new InvalidOperationException("Import batch must be ready before commit.");
        }

        Status = ImportBatchStatus.Committed;
        CommittedAt = DateTimeOffset.UtcNow;
        CommittedBy = NormalizeOptional(committedBy);
        Touch();
    }

    public void Cancel(string? cancelledBy = null)
    {
        if (Status == ImportBatchStatus.Committed)
        {
            throw new InvalidOperationException("Committed import batches cannot be cancelled.");
        }

        if (Status == ImportBatchStatus.Cancelled)
        {
            throw new InvalidOperationException("Import batch is already cancelled.");
        }

        Status = ImportBatchStatus.Cancelled;
        Touch();
    }

    private void EnsurePreviewEditable()
    {
        if (Status != ImportBatchStatus.Previewed)
        {
            throw new InvalidOperationException("Rows can only be added during preview.");
        }
    }

    private void EnsureNotFinal()
    {
        if (Status is ImportBatchStatus.Committed or ImportBatchStatus.Cancelled)
        {
            throw new InvalidOperationException("Final import batches cannot be validated.");
        }
    }

    private void Touch() => ConcurrencyToken++;

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

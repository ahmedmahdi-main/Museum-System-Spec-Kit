namespace MuseumSystem.Domain.Modules.Import;

public enum ImportRowStatus
{
    NeedsReview = 1,
    Accepted = 2,
    Rejected = 3
}

public sealed class ImportRow
{
    private ImportRow()
    {
    }

    private ImportRow(int rowNumber, string? categoryValue, string? itemNumberValue, string? locationValue, string? descriptionValue)
    {
        ImportRowId = Guid.NewGuid();
        RowNumber = rowNumber;
        CategoryValue = NormalizeOptional(categoryValue);
        ItemNumberValue = NormalizeOptional(itemNumberValue);
        LocationValue = NormalizeOptional(locationValue);
        DescriptionValue = NormalizeOptional(descriptionValue);
        Status = ImportRowStatus.NeedsReview;
        Issues = string.Empty;
    }

    public Guid ImportRowId { get; private set; }
    public Guid ImportBatchId { get; private set; }
    public ImportBatch? ImportBatch { get; private set; }
    public int RowNumber { get; private set; }
    public string? CategoryValue { get; private set; }
    public string? ItemNumberValue { get; private set; }
    public string? LocationValue { get; private set; }
    public string? DescriptionValue { get; private set; }
    public Guid? ProposedCategoryId { get; private set; }
    public Guid? ProposedLocationId { get; private set; }
    public Guid? ProposedArtifactId { get; private set; }
    public ImportRowStatus Status { get; private set; }
    public string Issues { get; private set; } = string.Empty;

    public static ImportRow Create(int rowNumber, string? categoryValue, string? itemNumberValue, string? locationValue, string? descriptionValue) =>
        new(rowNumber, categoryValue, itemNumberValue, locationValue, descriptionValue);

    public void Accept(Guid categoryId, Guid locationId)
    {
        ProposedCategoryId = categoryId;
        ProposedLocationId = locationId;
        Status = ImportRowStatus.Accepted;
        Issues = string.Empty;
    }

    public void Reject(IEnumerable<string> issues)
    {
        ProposedCategoryId = null;
        ProposedLocationId = null;
        Status = ImportRowStatus.Rejected;
        Issues = JoinIssues(issues);
    }

    public void NeedsReview(IEnumerable<string> issues, Guid? categoryId = null, Guid? locationId = null)
    {
        ProposedCategoryId = categoryId;
        ProposedLocationId = locationId;
        Status = ImportRowStatus.NeedsReview;
        Issues = JoinIssues(issues);
    }

    public void MarkCommittedArtifact(Guid artifactId)
    {
        ProposedArtifactId = artifactId;
    }

    private static string JoinIssues(IEnumerable<string> issues) => string.Join(" | ", issues.Where(issue => !string.IsNullOrWhiteSpace(issue)).Select(issue => issue.Trim()));

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

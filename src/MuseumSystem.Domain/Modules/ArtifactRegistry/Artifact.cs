using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Domain.Modules.ArtifactRegistry;

public enum ArtifactCurrentStatus
{
    InStorage = 1,
    OutOfStorage = 2
}

public sealed class Artifact
{
    private Artifact()
    {
    }

    private Artifact(ArtifactCategory category, int itemNumber, string basicDescription, Location initialLocation)
    {
        if (!category.IsActive)
        {
            throw new InvalidOperationException("Cannot create an artifact in an inactive category.");
        }

        if (!initialLocation.IsActive || initialLocation.LocationType != LocationType.Storage)
        {
            throw new InvalidOperationException("Initial location must be an active storage location.");
        }

        ArtifactId = Guid.NewGuid();
        CategoryId = category.CategoryId;
        Category = category;
        ItemNumber = RequirePositive(itemNumber);
        MuseumNumberDisplay = MuseumNumber.Create(category.CategoryCode, ItemNumber).DisplayValue;
        BasicDescription = NormalizeDescription(basicDescription);
        CurrentStatus = ArtifactCurrentStatus.InStorage;
        CurrentLocationId = initialLocation.LocationId;
        LastKnownStorageLocationId = initialLocation.LocationId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid ArtifactId { get; private set; }
    public Guid CategoryId { get; private set; }
    public ArtifactCategory? Category { get; private set; }
    public int ItemNumber { get; private set; }
    public string MuseumNumberDisplay { get; private set; } = string.Empty;
    public string BasicDescription { get; private set; } = string.Empty;
    public ArtifactCurrentStatus CurrentStatus { get; private set; }
    public Guid? CurrentLocationId { get; private set; }
    public Location? CurrentLocation { get; private set; }
    public string? CurrentHolderType { get; private set; }
    public string? CurrentHolderName { get; private set; }
    public Guid? LastKnownStorageLocationId { get; private set; }
    public Location? LastKnownStorageLocation { get; private set; }
    public Guid? CreatedFromImportBatchId { get; private set; }
    public uint ConcurrencyToken { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public static Artifact Create(ArtifactCategory category, int itemNumber, string basicDescription, Location initialLocation) =>
        new(category, itemNumber, basicDescription, initialLocation);

    public void UpdateBasicDescription(string basicDescription)
    {
        BasicDescription = NormalizeDescription(basicDescription);
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void RefreshMuseumNumber(string categoryCode)
    {
        MuseumNumberDisplay = MuseumNumber.Create(categoryCode, ItemNumber).DisplayValue;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    private static int RequirePositive(int value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Item number must be greater than zero.");
        }

        return value;
    }

    private static string NormalizeDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A basic description is required.", nameof(value));
        }

        return value.Trim();
    }
}

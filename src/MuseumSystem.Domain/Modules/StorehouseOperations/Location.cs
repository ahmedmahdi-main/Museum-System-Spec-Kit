namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public enum LocationType
{
    Storage = 1,
    DisplayHall = 2
}

public sealed class Location
{
    private Location()
    {
    }

    private Location(string nameArabic, LocationType locationType, Guid? parentLocationId)
    {
        LocationId = Guid.NewGuid();
        NameArabic = RequireText(nameArabic, nameof(nameArabic));
        LocationType = locationType;
        ParentLocationId = parentLocationId;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid LocationId { get; private set; }
    public string NameArabic { get; private set; } = string.Empty;
    public LocationType LocationType { get; private set; }
    public Guid? ParentLocationId { get; private set; }
    public Location? ParentLocation { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedAt { get; private set; }
    public string? LastModifiedBy { get; private set; }

    public static Location Create(string nameArabic, LocationType locationType, Guid? parentLocationId = null) =>
        new(nameArabic, locationType, parentLocationId);

    public void Update(string nameArabic, LocationType locationType, Guid? parentLocationId = null)
    {
        NameArabic = RequireText(nameArabic, nameof(nameArabic));
        LocationType = locationType;
        ParentLocationId = parentLocationId;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    public void DisableForNewUse()
    {
        IsActive = false;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    private static string RequireText(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", paramName);
        }

        return value.Trim();
    }
}

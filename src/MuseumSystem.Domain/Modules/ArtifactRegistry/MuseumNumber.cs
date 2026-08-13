namespace MuseumSystem.Domain.Modules.ArtifactRegistry;

public sealed record MuseumNumber
{
    private MuseumNumber(string categoryCode, int itemNumber)
    {
        CategoryCode = categoryCode;
        ItemNumber = itemNumber;
    }

    public string CategoryCode { get; }
    public int ItemNumber { get; }
    public string DisplayValue => $"{CategoryCode}-{ItemNumber}";

    public static MuseumNumber Create(string categoryCode, int itemNumber)
    {
        var normalizedCode = ArtifactCategory.NormalizeCategoryCode(categoryCode);
        if (itemNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(itemNumber), "Item number must be greater than zero.");
        }

        return new MuseumNumber(normalizedCode, itemNumber);
    }

    public override string ToString() => DisplayValue;
}


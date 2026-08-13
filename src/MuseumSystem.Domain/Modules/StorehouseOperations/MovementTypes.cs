namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public enum MovementType
{
    Delivery = 1,
    Return = 2
}

public enum MovementRecipientType
{
    DocumentationDivision = 1,
    LaboratoryDivision = 2,
    Photographer = 3,
    DisplayHall = 4
}

public sealed record MovementHolder(MovementRecipientType RecipientType, string Name)
{
    public static MovementHolder Create(MovementRecipientType recipientType, string? name)
    {
        var holderName = string.IsNullOrWhiteSpace(name) ? recipientType.ToString() : name.Trim();
        return new MovementHolder(recipientType, holderName);
    }
}

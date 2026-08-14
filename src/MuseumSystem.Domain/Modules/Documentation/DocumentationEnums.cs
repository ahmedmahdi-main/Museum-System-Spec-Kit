namespace MuseumSystem.Domain.Modules.Documentation;

public enum DocumentationFieldType
{
    Text = 1,
    MultilineText = 2,
    Number = 3,
    Date = 4,
    Boolean = 5,
    SingleSelect = 6,
    MultiSelect = 7
}

public enum DocumentationTemplateVersionStatus
{
    Draft = 1,
    Active = 2,
    Retired = 3
}

public enum DocumentationRecordStatus
{
    Draft = 1,
    Completed = 2
}

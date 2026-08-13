namespace MuseumSystem.Domain.Modules.IdentityAccess;

public sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    private AuditEntry(string? actorUserId, string actionName, string moduleName, string entityName, string entityId, string summary, string? changeSummary)
    {
        AuditEntryId = Guid.NewGuid();
        ActorUserId = NormalizeOptional(actorUserId);
        ActionName = RequireText(actionName, nameof(actionName));
        ModuleName = RequireText(moduleName, nameof(moduleName));
        EntityName = RequireText(entityName, nameof(entityName));
        EntityId = RequireText(entityId, nameof(entityId));
        OccurredAt = DateTimeOffset.UtcNow;
        Summary = RequireText(summary, nameof(summary));
        ChangeSummary = NormalizeOptional(changeSummary);
    }

    public Guid AuditEntryId { get; private set; }
    public string? ActorUserId { get; private set; }
    public string ActionName { get; private set; } = string.Empty;
    public string ModuleName { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string? ChangeSummary { get; private set; }

    public static AuditEntry Create(string? actorUserId, string actionName, string moduleName, string entityName, string entityId, string summary, string? changeSummary = null) =>
        new(actorUserId, actionName, moduleName, entityName, entityId, summary, changeSummary);

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

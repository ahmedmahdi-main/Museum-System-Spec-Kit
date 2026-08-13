namespace MuseumSystem.Application.Common.Audit;

public sealed record AuditActor(string? UserId, string DisplayName, bool IsAuthenticated)
{
    public static AuditActor System => new(null, "System", false);
}

public interface IAuditActorContext
{
    AuditActor CurrentActor { get; }
}

using MuseumSystem.Application.Common.Audit;

namespace MuseumSystem.Application.Modules.Documentation;

internal static class DocumentationActorIdentity
{
    public static string From(IAuditActorContext actorContext)
    {
        var actor = actorContext.CurrentActor;
        return string.IsNullOrWhiteSpace(actor.UserId) ? actor.DisplayName : actor.UserId;
    }
}
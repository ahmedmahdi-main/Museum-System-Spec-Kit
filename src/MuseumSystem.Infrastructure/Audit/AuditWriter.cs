using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.IdentityAccess;

namespace MuseumSystem.Infrastructure.Audit;

public sealed class AuditWriter(IMuseumDbContext dbContext, IAuditActorContext actorContext) : IAuditWriter
{
    public async Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        var actor = actorContext.CurrentActor;
        var entry = AuditEntry.Create(actor.UserId, request.ActionName, request.ModuleName, request.EntityName, request.EntityId, request.Summary, request.ChangeSummary);
        dbContext.AuditEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entry.AuditEntryId.ToString();
    }
}

public sealed class SystemAuditActorContext : IAuditActorContext
{
    public AuditActor CurrentActor => AuditActor.System;
}

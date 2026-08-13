using System.Security.Claims;
using Microsoft.AspNetCore.Http;
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

public sealed class HttpAuditActorContext(IHttpContextAccessor httpContextAccessor) : IAuditActorContext
{
    public AuditActor CurrentActor
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return AuditActor.System;
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var displayName = user.Identity.Name ?? user.FindFirstValue(ClaimTypes.Name) ?? "Authenticated user";
            return new AuditActor(userId, displayName, true);
        }
    }
}

public sealed class SystemAuditActorContext : IAuditActorContext
{
    public AuditActor CurrentActor => AuditActor.System;
}

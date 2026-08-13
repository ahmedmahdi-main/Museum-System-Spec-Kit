using MuseumSystem.Application.Common.Audit;

namespace MuseumSystem.Application.Tests.Audit;

public sealed class AuditActorContextTests
{
    [Fact]
    public void Audit_actor_represents_authenticated_staff_actor()
    {
        var actor = new AuditActor("user-1", "أمين المخزن", true);

        Assert.Equal("user-1", actor.UserId);
        Assert.Equal("أمين المخزن", actor.DisplayName);
        Assert.True(actor.IsAuthenticated);
    }

    [Fact]
    public void System_actor_is_available_for_seed_and_system_operations()
    {
        var actor = AuditActor.System;

        Assert.Null(actor.UserId);
        Assert.Equal("System", actor.DisplayName);
        Assert.False(actor.IsAuthenticated);
    }
}

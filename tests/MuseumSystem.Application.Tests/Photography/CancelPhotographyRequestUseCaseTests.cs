using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class CancelPhotographyRequestUseCaseTests
{
    [Fact]
    public async Task Original_requester_may_cancel_own_pending_request_without_manage_permission()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, requestedByUserId: "requester-1");
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: " requester-1 ",
            permissions: [],
            now: PhotographyRequestApplicationTestHost.CancelledAt);

        var result = await host.CancelUseCase.CancelPhotographyRequest(new CancelPhotographyRequestCommand(
            request.PhotographyRequestId,
            request.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var cancelled = await db.PhotographyRequests.SingleAsync();
        Assert.Equal(PhotographyRequestStatus.Cancelled, cancelled.Status);
        Assert.Equal("requester-1", cancelled.CancelledByUserId);
        Assert.Equal(PhotographyRequestApplicationTestHost.CancelledAt, cancelled.CancelledAt);
        Assert.Equal(1, cancelled.ConcurrencyToken);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.RequestCancel, audit.ActionName);
        Assert.Equal(cancelled.PhotographyRequestId.ToString(), audit.EntityId);
    }

    [Fact]
    public async Task Photography_manage_may_cancel_any_pending_request()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, requestedByUserId: "requester-1");
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: "manager-1",
            permissions: [PermissionNames.PhotographyManage],
            now: PhotographyRequestApplicationTestHost.CancelledAt);

        var result = await host.CancelUseCase.CancelPhotographyRequest(new CancelPhotographyRequestCommand(
            request.PhotographyRequestId,
            request.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var cancelled = await db.PhotographyRequests.SingleAsync();
        Assert.Equal(PhotographyRequestStatus.Cancelled, cancelled.Status);
        Assert.Equal("manager-1", cancelled.CancelledByUserId);
        Assert.Single(await db.AuditEntries.ToListAsync());
    }

    [Theory]
    [InlineData()]
    [InlineData(PermissionNames.PhotographyRequest)]
    [InlineData(PermissionNames.PhotographyUpload)]
    [InlineData(PermissionNames.PhotographyView)]
    public async Task Another_actor_without_manage_cannot_cancel_pending_request(params string[] permissions)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, requestedByUserId: "requester-1");
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: "other-user",
            permissions: permissions,
            now: PhotographyRequestApplicationTestHost.CancelledAt);

        var result = await host.CancelUseCase.CancelPhotographyRequest(new CancelPhotographyRequestCommand(
            request.PhotographyRequestId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        var unchanged = await db.PhotographyRequests.SingleAsync();
        Assert.Equal(PhotographyRequestStatus.Pending, unchanged.Status);
        Assert.Null(unchanged.CancelledByUserId);
        Assert.Null(unchanged.CancelledAt);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Completed_request_cannot_be_cancelled_and_records_no_success_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact);
        request.Complete(set.PhotographySetId, artifact.ArtifactId, PhotographyPurpose.GeneralDocumentation, true, "photographer-1", PhotographyRequestApplicationTestHost.CompletedAt);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: "requester-1",
            permissions: [PermissionNames.PhotographyManage],
            now: PhotographyRequestApplicationTestHost.CancelledAt);

        var result = await host.CancelUseCase.CancelPhotographyRequest(new CancelPhotographyRequestCommand(
            request.PhotographyRequestId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "PhotographyRequest.NotPending");
        var unchanged = await db.PhotographyRequests.SingleAsync();
        Assert.Equal(PhotographyRequestStatus.Completed, unchanged.Status);
        Assert.Null(unchanged.CancelledByUserId);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Cancelled_request_is_terminal_and_cannot_be_cancelled_again()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        request.Cancel("requester-1", PhotographyRequestApplicationTestHost.CancelledAt, actorHasManageAuthority: false);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: "requester-1",
            permissions: [PermissionNames.PhotographyManage]);

        var result = await host.CancelUseCase.CancelPhotographyRequest(new CancelPhotographyRequestCommand(
            request.PhotographyRequestId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "PhotographyRequest.NotPending");
        Assert.Equal(PhotographyRequestStatus.Cancelled, (await db.PhotographyRequests.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Stale_expected_concurrency_token_conflicts_without_mutation_or_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await host.CancelUseCase.CancelPhotographyRequest(new CancelPhotographyRequestCommand(
            request.PhotographyRequestId,
            request.ConcurrencyToken + 1));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(PhotographyRequestStatus.Pending, (await db.PhotographyRequests.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Db_concurrency_exception_maps_to_conflict_and_rolls_back_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        await db.SaveChangesAsync();
        var faultingContext = new FaultingPhotographyRequestDbContext(db) { ThrowNextRequestConcurrency = true };
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: "requester-1",
            permissions: [PermissionNames.PhotographyManage],
            persistenceContext: faultingContext);

        var result = await host.CancelUseCase.CancelPhotographyRequest(new CancelPhotographyRequestCommand(
            request.PhotographyRequestId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(1, faultingContext.RequestConcurrencyFailuresThrown);
        Assert.True(faultingContext.ClearTrackedChangesCalls > 0);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public void Cancel_command_does_not_accept_authoritative_actor_time_or_permission_inputs()
    {
        PhotographyRequestApplicationTestHost.AssertCommandShapeDoesNotExposeForbiddenInputs<CancelPhotographyRequestCommand>();
    }
}

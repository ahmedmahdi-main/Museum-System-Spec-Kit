using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class CompletePhotographyRequestUseCaseTests
{
    [Theory]
    [InlineData()]
    [InlineData(PermissionNames.PhotographyRequest)]
    [InlineData(PermissionNames.PhotographyManage)]
    [InlineData(PermissionNames.PhotographyView)]
    public async Task Complete_requires_exact_photography_upload_permission(params string[] permissions)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: "photographer-1",
            permissions: permissions,
            now: PhotographyRequestApplicationTestHost.CompletedAt);

        var result = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            request.PhotographyRequestId,
            set.PhotographySetId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Equal(PhotographyRequestStatus.Pending, (await db.PhotographyRequests.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Complete_rejects_unauthenticated_actor_before_mutation_or_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: " ",
            permissions: [PermissionNames.PhotographyUpload]);

        var result = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            request.PhotographyRequestId,
            set.PhotographySetId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.ActorRequired");
        Assert.Equal(PhotographyRequestStatus.Pending, (await db.PhotographyRequests.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Valid_complete_uses_authoritative_set_available_images_server_time_actor_and_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, PhotographyPurpose.PreMaintenance);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact, PhotographyPurpose.PreMaintenance);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: " photographer-1 ",
            permissions: [PermissionNames.PhotographyUpload],
            now: PhotographyRequestApplicationTestHost.CompletedAt);

        var result = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            request.PhotographyRequestId,
            set.PhotographySetId,
            request.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var completed = await db.PhotographyRequests.SingleAsync();
        Assert.Equal(PhotographyRequestStatus.Completed, completed.Status);
        Assert.Equal(set.PhotographySetId, completed.FulfillingPhotographySetId);
        Assert.Equal("photographer-1", completed.CompletedByUserId);
        Assert.Equal(PhotographyRequestApplicationTestHost.CompletedAt, completed.CompletedAt);
        Assert.Equal(1, completed.ConcurrencyToken);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.RequestComplete, audit.ActionName);
        Assert.Equal(completed.PhotographyRequestId.ToString(), audit.EntityId);
        Assert.Equal("photographer-1", audit.ActorUserId);
    }

    [Fact]
    public async Task Complete_rejects_set_from_different_artifact_or_different_purpose()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var otherArtifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, PhotographyPurpose.PreMaintenance);
        var wrongArtifactSet = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, otherArtifact, PhotographyPurpose.PreMaintenance);
        var wrongPurposeSet = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact, PhotographyPurpose.PostMaintenance);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(db, actorUserId: "photographer-1", permissions: [PermissionNames.PhotographyUpload]);

        var wrongArtifact = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            request.PhotographyRequestId,
            wrongArtifactSet.PhotographySetId,
            request.ConcurrencyToken));
        var wrongPurpose = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            request.PhotographyRequestId,
            wrongPurposeSet.PhotographySetId,
            request.ConcurrencyToken));

        Assert.False(wrongArtifact.Succeeded);
        Assert.Contains(wrongArtifact.ValidationIssues, issue => issue.Code == "PhotographySet.ArtifactConflict");
        Assert.False(wrongPurpose.Succeeded);
        Assert.Contains(wrongPurpose.ValidationIssues, issue => issue.Code == "PhotographySet.PurposeConflict");
        Assert.Equal(PhotographyRequestStatus.Pending, (await db.PhotographyRequests.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ArtifactImageStatus.DeletePending)]
    [InlineData(ArtifactImageStatus.Deleted)]
    public async Task Complete_requires_at_least_one_available_image(ArtifactImageStatus? imageStatus)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        var set = imageStatus.HasValue
            ? PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact, imageStatus: imageStatus.Value)
            : PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(db, actorUserId: "photographer-1", permissions: [PermissionNames.PhotographyUpload]);

        var result = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            request.PhotographyRequestId,
            set.PhotographySetId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "PhotographySet.AvailableImageRequired");
        Assert.Equal(PhotographyRequestStatus.Pending, (await db.PhotographyRequests.SingleAsync()).Status);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Many_requests_may_share_one_set_but_each_completes_independently()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var first = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        var second = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, requestedByUserId: "requester-2");
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(db, actorUserId: "photographer-1", permissions: [PermissionNames.PhotographyUpload]);

        var firstResult = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            first.PhotographyRequestId,
            set.PhotographySetId,
            first.ConcurrencyToken));

        Assert.True(firstResult.Succeeded);
        Assert.Equal(PhotographyRequestStatus.Completed, (await db.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == first.PhotographyRequestId)).Status);
        Assert.Equal(PhotographyRequestStatus.Pending, (await db.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == second.PhotographyRequestId)).Status);

        var secondRequest = await db.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == second.PhotographyRequestId);
        var secondResult = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            secondRequest.PhotographyRequestId,
            set.PhotographySetId,
            secondRequest.ConcurrencyToken));

        Assert.True(secondResult.Succeeded);
        Assert.Equal(set.PhotographySetId, (await db.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == first.PhotographyRequestId)).FulfillingPhotographySetId);
        Assert.Equal(set.PhotographySetId, (await db.PhotographyRequests.SingleAsync(request => request.PhotographyRequestId == second.PhotographyRequestId)).FulfillingPhotographySetId);
        Assert.Equal(2, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Terminal_or_stale_request_cannot_be_completed()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var completedRequest = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        var staleRequest = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, requestedByUserId: "requester-2");
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact);
        completedRequest.Complete(set.PhotographySetId, artifact.ArtifactId, PhotographyPurpose.GeneralDocumentation, true, "photographer-1", PhotographyRequestApplicationTestHost.CompletedAt);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(db, actorUserId: "photographer-1", permissions: [PermissionNames.PhotographyUpload]);

        var terminal = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            completedRequest.PhotographyRequestId,
            set.PhotographySetId,
            completedRequest.ConcurrencyToken));
        var stale = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            staleRequest.PhotographyRequestId,
            set.PhotographySetId,
            staleRequest.ConcurrencyToken + 1));

        Assert.False(terminal.Succeeded);
        Assert.Contains(terminal.ValidationIssues, issue => issue.Code == "PhotographyRequest.NotPending");
        Assert.False(stale.Succeeded);
        Assert.True(stale.ConcurrencyConflict);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Db_concurrency_exception_maps_to_conflict_without_success_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact);
        await db.SaveChangesAsync();
        var faultingContext = new FaultingPhotographyRequestDbContext(db) { ThrowNextRequestConcurrency = true };
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: "photographer-1",
            permissions: [PermissionNames.PhotographyUpload],
            persistenceContext: faultingContext);

        var result = await host.CompleteUseCase.CompletePhotographyRequest(new CompletePhotographyRequestCommand(
            request.PhotographyRequestId,
            set.PhotographySetId,
            request.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(1, faultingContext.RequestConcurrencyFailuresThrown);
        Assert.True(faultingContext.ClearTrackedChangesCalls > 0);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public void Complete_command_does_not_accept_authoritative_actor_time_available_image_or_permission_inputs()
    {
        PhotographyRequestApplicationTestHost.AssertCommandShapeDoesNotExposeForbiddenInputs<CompletePhotographyRequestCommand>();
        var memberNames = typeof(CompletePhotographyRequestCommand)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(memberNames, name => name.Contains("Available", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(memberNames, name => name.Contains("ImageCount", StringComparison.OrdinalIgnoreCase));
    }
}

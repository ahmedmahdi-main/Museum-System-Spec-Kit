using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class SetPrimaryArtifactImageUseCaseTests
{
    [Theory]
    [InlineData(PermissionNames.PhotographyManage)]
    public async Task Set_primary_succeeds_with_photography_manage_permission(string permission)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [permission]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            image.ArtifactImageId,
            0));

        Assert.True(result.Succeeded);
        Assert.Equal(image.ArtifactImageId, result.Value!.PrimaryImageId);
        Assert.Null(result.Value.PreviousPrimaryImageId);
        Assert.Equal(1, result.Value.ConcurrencyToken);
        var state = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(image.ArtifactImageId, state.PrimaryImageId);
        Assert.Equal("manager-1", state.UpdatedByUserId);
        Assert.Equal(1, state.ConcurrencyToken);
        Assert.Equal(PhotographyAuditActions.PrimaryImageChange, (await db.AuditEntries.SingleAsync()).ActionName);
    }

    [Theory]
    [InlineData()]
    [InlineData(PermissionNames.PhotographyView)]
    [InlineData(PermissionNames.PhotographyUpload)]
    [InlineData(PermissionNames.PhotographyRequest)]
    [InlineData(PermissionNames.PhotographyDelete)]
    public async Task Set_primary_requires_manage_and_does_not_mutate_or_audit(params string[] permissions)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: permissions);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            image.ArtifactImageId,
            0));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Equal(0, await db.ArtifactPhotographyStates.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Set_primary_requires_trusted_actor_and_does_not_mutate_or_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, actorUserId: " ", permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            image.ArtifactImageId,
            0));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.ActorRequired");
        Assert.Equal(0, await db.ArtifactPhotographyStates.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Set_primary_validates_artifact_image_and_expected_token_inputs()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var emptyArtifact = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(Guid.Empty, image.ArtifactImageId, 0));
        var missingArtifact = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(Guid.NewGuid(), image.ArtifactImageId, 0));
        var emptyImage = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(artifact.ArtifactId, Guid.Empty, 0));
        var missingImage = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(artifact.ArtifactId, Guid.NewGuid(), 0));
        var invalidToken = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(artifact.ArtifactId, image.ArtifactImageId, -1));

        Assert.Contains(emptyArtifact.ValidationIssues, issue => issue.Code == "Artifact.Required");
        Assert.Contains(missingArtifact.ValidationIssues, issue => issue.Code == "Artifact.NotFound");
        Assert.Contains(emptyImage.ValidationIssues, issue => issue.Code == "ArtifactImage.Required");
        Assert.Contains(missingImage.ValidationIssues, issue => issue.Code == "ArtifactImage.NotFound");
        Assert.Contains(invalidToken.ValidationIssues, issue => issue.Code == "ArtifactPhotographyState.ConcurrencyTokenInvalid");
        Assert.Equal(0, await db.ArtifactPhotographyStates.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Set_primary_rejects_target_from_different_artifact()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var (otherArtifact, otherImage) = SeedImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            otherImage.ArtifactImageId,
            0));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "PrimaryImage.ArtifactConflict");
        Assert.NotEqual(artifact.ArtifactId, otherArtifact.ArtifactId);
        Assert.Equal(0, await db.ArtifactPhotographyStates.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Theory]
    [InlineData(ArtifactImageStatus.DeletePending)]
    [InlineData(ArtifactImageStatus.Deleted)]
    public async Task Set_primary_rejects_delete_pending_or_deleted_target(ArtifactImageStatus status)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db, status);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            image.ArtifactImageId,
            0));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "PrimaryImage.ImageNotEligible");
        Assert.Equal(0, await db.ArtifactPhotographyStates.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task First_set_primary_creates_state_with_initial_expected_token_zero()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            image.ArtifactImageId,
            0));

        Assert.True(result.Succeeded);
        var state = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(artifact.ArtifactId, state.ArtifactId);
        Assert.Equal(image.ArtifactImageId, state.PrimaryImageId);
        Assert.Equal(1, state.ConcurrencyToken);
        Assert.Equal("manager-1", state.UpdatedByUserId);
        Assert.NotNull(result.AuditReference);
    }

    [Fact]
    public async Task First_set_primary_with_non_initial_expected_token_conflicts()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            image.ArtifactImageId,
            1));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(0, await db.ArtifactPhotographyStates.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Replacing_primary_changes_only_authoritative_state_and_audits_previous_to_new()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var imageA = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var imageB = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(imageA.ArtifactImageId, "manager-a");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            imageB.ArtifactImageId,
            state.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var finalState = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(imageB.ArtifactImageId, finalState.PrimaryImageId);
        Assert.Equal(2, finalState.ConcurrencyToken);
        Assert.Equal(2, await db.ArtifactImages.CountAsync());
        Assert.DoesNotContain(typeof(ArtifactImage).GetProperties(BindingFlags.Instance | BindingFlags.Public), property => property.Name == "IsPrimary");
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Contains($"PreviousPrimaryImageId={imageA.ArtifactImageId}", audit.ChangeSummary);
        Assert.Contains($"NewPrimaryImageId={imageB.ArtifactImageId}", audit.ChangeSummary);
    }

    [Fact]
    public async Task Same_primary_is_no_op_after_validations()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, image) = SeedImage(db);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(image.ArtifactImageId, "manager-1");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var token = state.ConcurrencyToken;
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            image.ArtifactImageId,
            token));

        Assert.True(result.Succeeded);
        var unchanged = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(image.ArtifactImageId, unchanged.PrimaryImageId);
        Assert.Equal(token, unchanged.ConcurrencyToken);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Stale_state_token_conflicts_without_mutation_or_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var imageA = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var imageB = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(imageA.ArtifactImageId, "manager-a");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            imageB.ArtifactImageId,
            state.ConcurrencyToken - 1));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Contains(result.Messages, message => message.Contains("ArtifactPhotographyState.ConcurrencyConflict", StringComparison.Ordinal));
        var unchanged = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(imageA.ArtifactImageId, unchanged.PrimaryImageId);
        Assert.Equal(state.ConcurrencyToken, unchanged.ConcurrencyToken);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Db_concurrency_exception_maps_to_conflict()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var imageA = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var imageB = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(imageA.ArtifactImageId, "manager-a");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var faulting = new FaultingPhotographyManagementDbContext(db) { ThrowNextStateConcurrency = true };
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage], persistenceContext: faulting);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            imageB.ArtifactImageId,
            state.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(1, faulting.StateConcurrencyFailuresThrown);
        Assert.True(faulting.ClearTrackedChangesCalls > 0);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task First_state_creation_race_maps_provider_neutral_db_update_to_conflict_after_reload()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var requested = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var competing = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        await db.SaveChangesAsync();
        var faulting = new FaultingPhotographyManagementDbContext(db)
        {
            ThrowNextFirstStateCreateRace = true,
            CompetingPrimaryImageId = competing.ArtifactImageId
        };
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage], persistenceContext: faulting);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            requested.ArtifactImageId,
            0));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(1, faulting.FirstStateCreateRaceFailuresThrown);
        var authoritative = await db.ArtifactPhotographyStates.SingleAsync();
        Assert.Equal(competing.ArtifactImageId, authoritative.PrimaryImageId);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Primary_change_audit_includes_artifact_previous_new_actor_and_server_timestamp_without_storage()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var imageA = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var imageB = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set);
        var state = ArtifactPhotographyState.Create(artifact.ArtifactId);
        state.SetPrimaryImage(imageA.ArtifactImageId, "manager-a");
        db.ArtifactPhotographyStates.Add(state);
        await db.SaveChangesAsync();
        var changedAt = new DateTimeOffset(2026, 8, 25, 9, 45, 0, TimeSpan.Zero);
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage], now: changedAt);

        var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
            artifact.ArtifactId,
            imageB.ArtifactImageId,
            state.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.PrimaryImageChange, audit.ActionName);
        Assert.Equal(artifact.ArtifactId.ToString(), audit.EntityId);
        Assert.Equal("manager-1", audit.ActorUserId);
        Assert.Contains($"ArtifactId={artifact.ArtifactId}", audit.ChangeSummary);
        Assert.Contains($"PreviousPrimaryImageId={imageA.ArtifactImageId}", audit.ChangeSummary);
        Assert.Contains($"NewPrimaryImageId={imageB.ArtifactImageId}", audit.ChangeSummary);
        Assert.Contains("ActingUserId=manager-1", audit.ChangeSummary);
        Assert.Contains($"ChangedAtUtc={changedAt:O}", audit.ChangeSummary);
        Assert.DoesNotContain("ObjectKey", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Set_primary_command_does_not_accept_actor_role_timestamp_current_primary_or_storage_data()
    {
        PhotographyRequestApplicationTestHost.AssertCommandShapeDoesNotExposeForbiddenInputs<SetPrimaryArtifactImageCommand>();
        UpdateArtifactImageMetadataUseCaseTests.AssertNoForbiddenMembers(typeof(SetPrimaryArtifactImageCommand), [
            "Role",
            "CurrentPrimary",
            "UpdatedAt",
            "ChangedAt",
            "AuditTimestamp",
            "ObjectKey",
            "Storage",
            "Bucket",
            "Endpoint"]);
    }

    internal static SetPrimaryArtifactImageUseCase NewUseCase(
        MuseumDbContext db,
        string actorUserId = "manager-1",
        IReadOnlyCollection<string>? permissions = null,
        DateTimeOffset? now = null,
        IMuseumDbContext? persistenceContext = null)
    {
        var context = persistenceContext ?? db;
        var actorContext = new TestAuditActorContext(actorUserId, "Photography Manager");
        var stateService = new ArtifactPhotographyStateService(context);
        return new SetPrimaryArtifactImageUseCase(
            context,
            actorContext,
            new FakeCurrentActorPermissionChecker(permissions ?? []),
            new AuditWriter(context, actorContext),
            stateService,
            new FixedTimeProvider(now ?? PhotographyRequestApplicationTestHost.CompletedAt));
    }

    internal static (Artifact Artifact, ArtifactImage Image) SeedImage(
        MuseumDbContext db,
        ArtifactImageStatus status = ArtifactImageStatus.Available)
    {
        var (artifact, _, image) = UpdateArtifactImageMetadataUseCaseTests.SeedAvailableImage(db, status);
        return (artifact, image);
    }
}

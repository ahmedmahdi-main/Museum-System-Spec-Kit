using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Photography;

public sealed class CreatePhotographyRequestUseCaseTests
{
    [Theory]
    [InlineData()]
    [InlineData(PermissionNames.PhotographyUpload)]
    [InlineData(PermissionNames.PhotographyManage)]
    [InlineData(PermissionNames.PhotographyView)]
    public async Task Create_requires_exact_photography_request_permission(params string[] permissions)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(db, permissions: permissions);

        var result = await host.CreateUseCase.CreatePhotographyRequest(new CreatePhotographyRequestCommand(
            artifact.ArtifactId,
            PhotographyPurpose.GeneralDocumentation));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        Assert.Equal(0, await db.PhotographyRequests.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Create_rejects_unauthenticated_actor_before_persistence_or_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: " ",
            permissions: [PermissionNames.PhotographyRequest]);

        var result = await host.CreateUseCase.CreatePhotographyRequest(new CreatePhotographyRequestCommand(
            artifact.ArtifactId,
            PhotographyPurpose.GeneralDocumentation));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.ActorRequired");
        Assert.Equal(0, await db.PhotographyRequests.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Create_requires_existing_artifact_before_creating_request_or_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(db, permissions: [PermissionNames.PhotographyRequest]);

        var result = await host.CreateUseCase.CreatePhotographyRequest(new CreatePhotographyRequestCommand(
            Guid.NewGuid(),
            PhotographyPurpose.GeneralDocumentation));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Artifact.NotFound");
        Assert.Equal(0, await db.PhotographyRequests.CountAsync());
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Valid_create_persists_pending_defaults_trusted_actor_server_time_and_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        await db.SaveChangesAsync();
        var host = PhotographyRequestApplicationTestHost.CreateUseCases(
            db,
            actorUserId: " requester-1 ",
            permissions: [PermissionNames.PhotographyRequest],
            now: PhotographyRequestApplicationTestHost.RequestedAt);

        var result = await host.CreateUseCase.CreatePhotographyRequest(new CreatePhotographyRequestCommand(
            artifact.ArtifactId,
            PhotographyPurpose.PreMaintenance));

        Assert.True(result.Succeeded);
        Assert.Equal("Photography request created.", Assert.Single(result.Messages));
        Assert.NotNull(result.AuditReference);
        var request = await db.PhotographyRequests.SingleAsync();
        Assert.Equal(result.Value!.PhotographyRequestId, request.PhotographyRequestId);
        Assert.Equal(artifact.ArtifactId, request.ArtifactId);
        Assert.Equal(PhotographyPurpose.PreMaintenance, request.Purpose);
        Assert.Equal("requester-1", request.RequestedByUserId);
        Assert.Equal(PhotographyRequestApplicationTestHost.RequestedAt, request.RequestedAt);
        Assert.Equal(PhotographyRequestStatus.Pending, request.Status);
        Assert.Null(request.FulfillingPhotographySetId);
        Assert.Null(request.CompletedByUserId);
        Assert.Null(request.CompletedAt);
        Assert.Null(request.CancelledByUserId);
        Assert.Null(request.CancelledAt);
        Assert.Equal(0, request.ConcurrencyToken);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.RequestCreate, audit.ActionName);
        Assert.Equal("Photography", audit.ModuleName);
        Assert.Equal(nameof(PhotographyRequest), audit.EntityName);
        Assert.Equal(request.PhotographyRequestId.ToString(), audit.EntityId);
        Assert.Equal("requester-1", audit.ActorUserId);
    }

    [Fact]
    public async Task Request_queries_join_current_artifact_summary_without_request_owned_artifact_snapshots()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact);
        await db.SaveChangesAsync();
        artifact.UpdateBasicDescription("Updated central artifact description");
        await db.SaveChangesAsync();
        var queries = new PhotographyRequestQueries(db);

        var detail = await queries.GetPhotographyRequestDetail(request.PhotographyRequestId);

        Assert.True(detail.Succeeded);
        Assert.Equal(request.PhotographyRequestId, detail.Value!.Request.PhotographyRequestId);
        Assert.Equal(artifact.MuseumNumberDisplay, detail.Value.Artifact.MuseumNumber);
        Assert.Equal("Updated central artifact description", detail.Value.Artifact.BasicDescription);
        Assert.DoesNotContain(EntityMemberNames<PhotographyRequest>(), name =>
            name.Contains("MuseumNumber", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Category", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Custody", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Movement", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Location", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Documentation", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Laboratory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Request_queries_filter_order_and_expose_no_object_storage_internals()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var later = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, requestedAt: PhotographyRequestApplicationTestHost.RequestedAt.AddMinutes(1));
        var earlier = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, purpose: PhotographyPurpose.PostMaintenance);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact, PhotographyPurpose.PostMaintenance);
        earlier.Complete(set.PhotographySetId, artifact.ArtifactId, PhotographyPurpose.PostMaintenance, true, "photographer-1", PhotographyRequestApplicationTestHost.CompletedAt);
        await db.SaveChangesAsync();
        var queries = new PhotographyRequestQueries(db);

        var pending = await queries.ListPhotographyRequests(new PhotographyRequestListQuery(PhotographyRequestStatus.Pending));
        var completed = await queries.ListPhotographyRequests(new PhotographyRequestListQuery(PhotographyRequestStatus.Completed));

        Assert.Equal([later.PhotographyRequestId], pending.Select(summary => summary.Request.PhotographyRequestId));
        Assert.Equal([earlier.PhotographyRequestId], completed.Select(summary => summary.Request.PhotographyRequestId));
        Assert.Equal(set.PhotographySetId, completed.Single().FulfillingSet!.PhotographySetId);
        Assert.Equal(1, completed.Single().FulfillingSet!.AvailableImageCount);
        Assert.DoesNotContain(RequestDtoPropertyNames(), name =>
            name.Contains("ObjectKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Bucket", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Endpoint", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Presigned", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Minio", StringComparison.OrdinalIgnoreCase)
            || name.Contains("OpaqueAccess", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Eligible_fulfilling_set_discovery_is_scoped_to_request_artifact_purpose_and_available_images()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var otherArtifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var request = PhotographyRequestApplicationTestHost.AddRequest(db, artifact, PhotographyPurpose.PreMaintenance);
        var eligible = PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact, PhotographyPurpose.PreMaintenance);
        PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, artifact, PhotographyPurpose.PostMaintenance);
        PhotographyRequestApplicationTestHost.AddPhotographySetWithImage(db, otherArtifact, PhotographyPurpose.PreMaintenance);
        PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact, PhotographyPurpose.PreMaintenance);
        await db.SaveChangesAsync();
        var queries = new PhotographyRequestQueries(db);

        var result = await queries.ListEligibleFulfillingSetsForRequest(request.PhotographyRequestId);

        Assert.True(result.Succeeded);
        var candidate = Assert.Single(result.Value!);
        Assert.Equal(eligible.PhotographySetId, candidate.PhotographySetId);
        Assert.Equal(artifact.ArtifactId, candidate.ArtifactId);
        Assert.Equal(PhotographyPurpose.PreMaintenance, candidate.Purpose);
        Assert.Equal(1, candidate.AvailableImageCount);
    }

    [Fact]
    public void Create_command_does_not_accept_authoritative_actor_time_or_permission_inputs()
    {
        PhotographyRequestApplicationTestHost.AssertCommandShapeDoesNotExposeForbiddenInputs<CreatePhotographyRequestCommand>();
    }

    private static IReadOnlyList<string> EntityMemberNames<T>() =>
        typeof(T)
            .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(member => member.Name)
            .ToArray();

    private static IReadOnlyList<string> RequestDtoPropertyNames() =>
    [
        .. typeof(PhotographyRequestDto).GetProperties().Select(property => property.Name),
        .. typeof(PhotographyRequestArtifactSummaryDto).GetProperties().Select(property => property.Name),
        .. typeof(PhotographyRequestFulfillingSetSummaryDto).GetProperties().Select(property => property.Name),
        .. typeof(PhotographyRequestSummaryDto).GetProperties().Select(property => property.Name)
    ];
}

internal static class PhotographyRequestApplicationTestHost
{
    public static readonly DateTimeOffset RequestedAt = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset CancelledAt = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset CompletedAt = new(2026, 8, 24, 11, 0, 0, TimeSpan.Zero);

    public static MuseumDbContext CreateDbContext() =>
        PhotographyUploadApplicationTestHost.CreateDbContext();

    public static Artifact AddArtifact(MuseumDbContext db) =>
        PhotographyUploadApplicationTestHost.AddArtifact(db);

    public static PhotographyRequest AddRequest(
        MuseumDbContext db,
        Artifact artifact,
        PhotographyPurpose purpose = PhotographyPurpose.GeneralDocumentation,
        string requestedByUserId = "requester-1",
        DateTimeOffset? requestedAt = null)
    {
        var request = PhotographyRequest.Create(artifact.ArtifactId, purpose, requestedByUserId, requestedAt ?? RequestedAt);
        db.PhotographyRequests.Add(request);
        return request;
    }

    public static PhotographySet AddPhotographySet(
        MuseumDbContext db,
        Artifact artifact,
        PhotographyPurpose purpose = PhotographyPurpose.GeneralDocumentation)
    {
        var set = PhotographySet.Create(artifact.ArtifactId, purpose, new DateOnly(2026, 8, 24), "photographer-1", "photographer-1");
        db.PhotographySets.Add(set);
        return set;
    }

    public static PhotographySet AddPhotographySetWithImage(
        MuseumDbContext db,
        Artifact artifact,
        PhotographyPurpose purpose = PhotographyPurpose.GeneralDocumentation,
        ArtifactImageStatus imageStatus = ArtifactImageStatus.Available)
    {
        var set = AddPhotographySet(db, artifact, purpose);
        AddImage(db, artifact, set, imageStatus);
        return set;
    }

    public static ArtifactImage AddImage(
        MuseumDbContext db,
        Artifact artifact,
        PhotographySet set,
        ArtifactImageStatus status = ArtifactImageStatus.Available)
    {
        var image = ArtifactImage.Create(
            artifact.ArtifactId,
            set.PhotographySetId,
            ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/original.jpg"),
            "front.jpg",
            "image/jpeg",
            128,
            800,
            600,
            "photographer-1",
            CompletedAt);

        if (status == ArtifactImageStatus.DeletePending)
        {
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
        }
        else if (status == ArtifactImageStatus.Deleted)
        {
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod, "photographer-1", new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero));
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod);
        }

        db.ArtifactImages.Add(image);
        return image;
    }

    public static PhotographyRequestUseCaseHost CreateUseCases(
        MuseumDbContext db,
        string actorUserId = "requester-1",
        IReadOnlyCollection<string>? permissions = null,
        DateTimeOffset? now = null,
        IMuseumDbContext? persistenceContext = null)
    {
        var actorContext = new TestAuditActorContext(actorUserId);
        var context = persistenceContext ?? db;
        var auditWriter = new AuditWriter(context, actorContext);
        var permissionChecker = new FakeCurrentActorPermissionChecker(permissions ?? []);
        var clock = new FixedTimeProvider(now ?? RequestedAt);
        return new PhotographyRequestUseCaseHost(
            new CreatePhotographyRequestUseCase(context, actorContext, permissionChecker, auditWriter, clock),
            new CancelPhotographyRequestUseCase(context, actorContext, permissionChecker, auditWriter, clock),
            new CompletePhotographyRequestUseCase(context, actorContext, permissionChecker, auditWriter, clock),
            new PhotographyRequestQueries(context),
            permissionChecker,
            clock);
    }

    public static void AssertCommandShapeDoesNotExposeForbiddenInputs<TCommand>()
    {
        var forbiddenFragments = new[]
        {
            "Actor",
            "UserId",
            "RequestedAt",
            "CompletedAt",
            "CancelledAt",
            "Permission",
            "Can",
            "HasManage"
        };
        var memberNames = typeof(TCommand)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.Name ?? string.Empty))
            .Concat(typeof(TCommand).GetProperties().Select(property => property.Name))
            .ToArray();

        foreach (var forbiddenFragment in forbiddenFragments)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(forbiddenFragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}

internal sealed record PhotographyRequestUseCaseHost(
    CreatePhotographyRequestUseCase CreateUseCase,
    CancelPhotographyRequestUseCase CancelUseCase,
    CompletePhotographyRequestUseCase CompleteUseCase,
    PhotographyRequestQueries Queries,
    FakeCurrentActorPermissionChecker PermissionChecker,
    FixedTimeProvider Clock);

internal sealed class FakeCurrentActorPermissionChecker(IReadOnlyCollection<string> permissions) : ICurrentActorPermissionChecker
{
    private readonly HashSet<string> permissions = new(permissions, StringComparer.Ordinal);

    public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

internal sealed class FaultingPhotographyRequestDbContext(MuseumDbContext inner) : IMuseumDbContext
{
    public bool ThrowNextRequestConcurrency { get; set; }
    public int RequestConcurrencyFailuresThrown { get; private set; }
    public int ClearTrackedChangesCalls { get; private set; }

    public DbSet<ArtifactCategory> ArtifactCategories => inner.ArtifactCategories;
    public DbSet<Artifact> Artifacts => inner.Artifacts;
    public DbSet<Location> Locations => inner.Locations;
    public DbSet<MovementRecord> MovementRecords => inner.MovementRecords;
    public DbSet<ImportBatch> ImportBatches => inner.ImportBatches;
    public DbSet<ImportRow> ImportRows => inner.ImportRows;
    public DbSet<ReconciliationSession> ReconciliationSessions => inner.ReconciliationSessions;
    public DbSet<ReconciliationResult> ReconciliationResults => inner.ReconciliationResults;
    public DbSet<DocumentedCorrection> DocumentedCorrections => inner.DocumentedCorrections;
    public DbSet<AuditEntry> AuditEntries => inner.AuditEntries;
    public DbSet<DocumentationTemplate> DocumentationTemplates => inner.DocumentationTemplates;
    public DbSet<DocumentationTemplateVersion> DocumentationTemplateVersions => inner.DocumentationTemplateVersions;
    public DbSet<DocumentationTemplateField> DocumentationTemplateFields => inner.DocumentationTemplateFields;
    public DbSet<DocumentationTemplateFieldOption> DocumentationTemplateFieldOptions => inner.DocumentationTemplateFieldOptions;
    public DbSet<DocumentationRecord> DocumentationRecords => inner.DocumentationRecords;
    public DbSet<DocumentationRevision> DocumentationRevisions => inner.DocumentationRevisions;
    public DbSet<PhotographyRequest> PhotographyRequests => inner.PhotographyRequests;
    public DbSet<PhotographySet> PhotographySets => inner.PhotographySets;
    public DbSet<ArtifactImage> ArtifactImages => inner.ArtifactImages;
    public DbSet<ArtifactImageDerivative> ArtifactImageDerivatives => inner.ArtifactImageDerivatives;
    public DbSet<ArtifactPhotographyState> ArtifactPhotographyStates => inner.ArtifactPhotographyStates;
    public DbSet<PhotographyUploadOperation> PhotographyUploadOperations => inner.PhotographyUploadOperations;
    public DbSet<PhotographyUploadFileOutcome> PhotographyUploadFileOutcomes => inner.PhotographyUploadFileOutcomes;
    public DbSet<StorageOperationRecovery> StorageOperationRecoveries => inner.StorageOperationRecoveries;

    public Task<IMuseumDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        inner.BeginTransactionAsync(cancellationToken);

    public void ClearTrackedChanges()
    {
        ClearTrackedChangesCalls++;
        inner.ClearTrackedChanges();
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var modifiedRequest = inner.ChangeTracker.Entries<PhotographyRequest>()
            .FirstOrDefault(entry => entry.State == EntityState.Modified);
        if (ThrowNextRequestConcurrency && modifiedRequest is not null)
        {
            ThrowNextRequestConcurrency = false;
            RequestConcurrencyFailuresThrown++;
            throw new DbUpdateConcurrencyException("Simulated concurrent photography request update.");
        }

        return await inner.SaveChangesAsync(cancellationToken);
    }
}

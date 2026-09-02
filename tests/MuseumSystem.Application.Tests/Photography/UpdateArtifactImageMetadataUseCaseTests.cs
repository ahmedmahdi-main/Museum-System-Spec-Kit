using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

public sealed class UpdateArtifactImageMetadataUseCaseTests
{
    [Theory]
    [InlineData(PermissionNames.PhotographyManage)]
    public async Task Metadata_update_succeeds_with_photography_manage_permission(string permission)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (artifact, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [permission]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            " Updated caption ",
            image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal("Updated caption", result.Value!.Caption);
        Assert.Equal(artifact.ArtifactId, result.Value.ArtifactId);
        Assert.Equal(1, result.Value.ConcurrencyToken);
        var updated = await db.ArtifactImages.SingleAsync(candidate => candidate.ArtifactImageId == image.ArtifactImageId);
        Assert.Equal("Updated caption", updated.Caption);
        Assert.Equal(1, updated.ConcurrencyToken);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.ImageMetadataChange, audit.ActionName);
        Assert.Equal(image.ArtifactImageId.ToString(), audit.EntityId);
        Assert.Equal("manager-1", audit.ActorUserId);
    }

    [Theory]
    [InlineData()]
    [InlineData(PermissionNames.PhotographyView)]
    [InlineData(PermissionNames.PhotographyUpload)]
    [InlineData(PermissionNames.PhotographyRequest)]
    [InlineData(PermissionNames.PhotographyDelete)]
    public async Task Metadata_update_requires_manage_and_does_not_mutate_or_audit(params string[] permissions)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        image.UpdateCaption("Before");
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: permissions);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "After",
            image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.PermissionDenied");
        var unchanged = await db.ArtifactImages.SingleAsync(candidate => candidate.ArtifactImageId == image.ArtifactImageId);
        Assert.Equal("Before", unchanged.Caption);
        Assert.Equal(image.ConcurrencyToken, unchanged.ConcurrencyToken);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Metadata_update_requires_trusted_actor_and_does_not_mutate_or_audit()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, actorUserId: " ", permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "After",
            image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "Photography.ActorRequired");
        Assert.Null((await db.ArtifactImages.SingleAsync()).Caption);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  trimmed  ", "trimmed")]
    public async Task Caption_normalization_reuses_domain_behavior(string? caption, string? expected)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        image.UpdateCaption("Before");
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            caption,
            image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal(expected, (await db.ArtifactImages.SingleAsync()).Caption);
    }

    [Fact]
    public async Task Caption_longer_than_1000_is_rejected_before_database_provider_errors()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            new string('x', 1001),
            image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.CaptionTooLong");
        Assert.Null((await db.ArtifactImages.SingleAsync()).Caption);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Theory]
    [InlineData(ArtifactImageStatus.DeletePending)]
    [InlineData(ArtifactImageStatus.Deleted)]
    public async Task Non_available_images_are_not_editable(ArtifactImageStatus status)
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, status: status);
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "After",
            image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Code == "ArtifactImage.NotEditable");
        Assert.Null((await db.ArtifactImages.SingleAsync()).Caption);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Missing_image_and_invalid_inputs_return_stable_failures()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var emptyId = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(Guid.Empty, "Caption", 0));
        var missing = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(Guid.NewGuid(), "Caption", 0));
        var invalidToken = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(Guid.NewGuid(), "Caption", -1));

        Assert.Contains(emptyId.ValidationIssues, issue => issue.Code == "ArtifactImage.Required");
        Assert.Contains(missing.ValidationIssues, issue => issue.Code == "ArtifactImage.NotFound");
        Assert.Contains(invalidToken.ValidationIssues, issue => issue.Code == "ArtifactImage.ConcurrencyTokenInvalid");
    }

    [Fact]
    public async Task Stale_expected_token_conflicts_before_no_op_or_mutation()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        image.UpdateCaption("Current");
        await db.SaveChangesAsync();
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "Current",
            image.ConcurrencyToken - 1));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Contains(result.Messages, message => message.Contains("ArtifactImage.ConcurrencyConflict", StringComparison.Ordinal));
        Assert.Equal("Current", (await db.ArtifactImages.SingleAsync()).Caption);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Db_concurrency_exception_maps_to_conflict_and_clears_tracking()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        await db.SaveChangesAsync();
        var faulting = new FaultingPhotographyManagementDbContext(db) { ThrowNextImageConcurrency = true };
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage], persistenceContext: faulting);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "After",
            image.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(1, faulting.ImageConcurrencyFailuresThrown);
        Assert.True(faulting.ClearTrackedChangesCalls > 0);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Same_caption_is_no_op_after_concurrency_check()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        image.UpdateCaption("Current");
        await db.SaveChangesAsync();
        var token = image.ConcurrencyToken;
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "Current",
            token));

        Assert.True(result.Succeeded);
        var unchanged = await db.ArtifactImages.SingleAsync();
        Assert.Equal(token, unchanged.ConcurrencyToken);
        Assert.Equal("Current", unchanged.Caption);
        Assert.Equal(0, await db.AuditEntries.CountAsync());
    }

    [Fact]
    public async Task Caption_update_does_not_mutate_original_binary_or_derivatives()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db, addDerivatives: true);
        await db.SaveChangesAsync();
        var before = OriginalBinarySnapshot.From(image);
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage]);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "Museum label",
            image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var after = await db.ArtifactImages.Include(candidate => candidate.Derivatives).SingleAsync();
        before.AssertUnchangedExceptCaptionAndToken(after);
        Assert.Equal("Museum label", after.Caption);
        Assert.Equal(before.ConcurrencyToken + 1, after.ConcurrencyToken);
    }

    [Fact]
    public async Task Material_change_audit_identifies_artifact_image_captions_actor_and_server_time_without_storage()
    {
        await using var db = PhotographyRequestApplicationTestHost.CreateDbContext();
        var (_, _, image) = SeedAvailableImage(db);
        image.UpdateCaption("Before");
        await db.SaveChangesAsync();
        var changedAt = new DateTimeOffset(2026, 8, 25, 8, 30, 0, TimeSpan.Zero);
        var useCase = NewUseCase(db, permissions: [PermissionNames.PhotographyManage], now: changedAt);

        var result = await useCase.UpdateArtifactImageMetadata(new UpdateArtifactImageMetadataCommand(
            image.ArtifactImageId,
            "After",
            image.ConcurrencyToken));

        Assert.True(result.Succeeded);
        var audit = await db.AuditEntries.SingleAsync();
        Assert.Equal(PhotographyAuditActions.ImageMetadataChange, audit.ActionName);
        Assert.Contains($"ArtifactId={image.ArtifactId}", audit.ChangeSummary);
        Assert.Contains($"ArtifactImageId={image.ArtifactImageId}", audit.ChangeSummary);
        Assert.Contains("PreviousCaption=Before", audit.ChangeSummary);
        Assert.Contains("NewCaption=After", audit.ChangeSummary);
        Assert.Contains("ActingUserId=manager-1", audit.ChangeSummary);
        Assert.Contains($"ChangedAtUtc={changedAt:O}", audit.ChangeSummary);
        Assert.DoesNotContain("ObjectKey", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bucket", audit.ChangeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Metadata_command_and_summary_do_not_expose_untrusted_actor_timestamp_or_storage_inputs()
    {
        PhotographyRequestApplicationTestHost.AssertCommandShapeDoesNotExposeForbiddenInputs<UpdateArtifactImageMetadataCommand>();
        AssertNoForbiddenMembers(typeof(UpdateArtifactImageMetadataCommand), [
            "UpdatedBy",
            "ChangedAt",
            "AuditTimestamp",
            "ObjectKey",
            "Filename",
            "ContentType",
            "Stream",
            "Bytes",
            "Bucket",
            "Endpoint",
            "Minio"]);
        AssertNoForbiddenMembers(typeof(ArtifactImageMetadataManagementDto), [
            "ObjectKey",
            "Bucket",
            "Endpoint",
            "Presigned",
            "Minio"]);
    }

    internal static UpdateArtifactImageMetadataUseCase NewUseCase(
        MuseumDbContext db,
        string actorUserId = "manager-1",
        IReadOnlyCollection<string>? permissions = null,
        DateTimeOffset? now = null,
        IMuseumDbContext? persistenceContext = null)
    {
        var context = persistenceContext ?? db;
        var actorContext = new TestAuditActorContext(actorUserId, "Photography Manager");
        return new UpdateArtifactImageMetadataUseCase(
            context,
            actorContext,
            new FakeCurrentActorPermissionChecker(permissions ?? []),
            new AuditWriter(context, actorContext),
            new FixedTimeProvider(now ?? PhotographyRequestApplicationTestHost.CompletedAt));
    }

    internal static (Artifact Artifact, PhotographySet Set, ArtifactImage Image) SeedAvailableImage(
        MuseumDbContext db,
        ArtifactImageStatus status = ArtifactImageStatus.Available,
        bool addDerivatives = false)
    {
        var artifact = PhotographyRequestApplicationTestHost.AddArtifact(db);
        var set = PhotographyRequestApplicationTestHost.AddPhotographySet(db, artifact);
        var image = PhotographyRequestApplicationTestHost.AddImage(db, artifact, set, status);

        if (addDerivatives)
        {
            image.AddDerivative(ArtifactImageDerivative.Create(
                image.ArtifactImageId,
                ImageDerivativeKind.Thumbnail,
                ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/thumbnail.jpg"),
                "image/jpeg",
                32,
                120,
                90));
            image.AddDerivative(ArtifactImageDerivative.Create(
                image.ArtifactImageId,
                ImageDerivativeKind.Preview,
                ImageStorageObjectKey.Create($"artifact-images/{Guid.NewGuid():N}/preview.jpg"),
                "image/jpeg",
                64,
                640,
                480));
        }

        return (artifact, set, image);
    }

    internal static void AssertNoForbiddenMembers(Type type, IReadOnlyCollection<string> forbiddenFragments)
    {
        var memberNames = type
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.Name ?? string.Empty))
            .Concat(type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(property => property.Name))
            .ToArray();

        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(memberNames, name => name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed record OriginalBinarySnapshot(
        Guid ArtifactId,
        Guid PhotographySetId,
        ImageStorageObjectKey OriginalObjectKey,
        string OriginalFilename,
        string ContentType,
        long FileSizeBytes,
        int PixelWidth,
        int PixelHeight,
        string UploadedByUserId,
        DateTimeOffset UploadedAt,
        int ConcurrencyToken,
        IReadOnlyList<Guid> DerivativeIds)
    {
        public static OriginalBinarySnapshot From(ArtifactImage image) =>
            new(
                image.ArtifactId,
                image.PhotographySetId,
                image.OriginalObjectKey,
                image.OriginalFilename,
                image.ContentType,
                image.FileSizeBytes,
                image.PixelWidth,
                image.PixelHeight,
                image.UploadedByUserId,
                image.UploadedAt,
                image.ConcurrencyToken,
                image.Derivatives.Select(derivative => derivative.ArtifactImageDerivativeId).Order().ToArray());

        public void AssertUnchangedExceptCaptionAndToken(ArtifactImage image)
        {
            Assert.Equal(ArtifactId, image.ArtifactId);
            Assert.Equal(PhotographySetId, image.PhotographySetId);
            Assert.Equal(OriginalObjectKey, image.OriginalObjectKey);
            Assert.Equal(OriginalFilename, image.OriginalFilename);
            Assert.Equal(ContentType, image.ContentType);
            Assert.Equal(FileSizeBytes, image.FileSizeBytes);
            Assert.Equal(PixelWidth, image.PixelWidth);
            Assert.Equal(PixelHeight, image.PixelHeight);
            Assert.Equal(UploadedByUserId, image.UploadedByUserId);
            Assert.Equal(UploadedAt, image.UploadedAt);
            Assert.Equal(DerivativeIds, image.Derivatives.Select(derivative => derivative.ArtifactImageDerivativeId).Order().ToArray());
        }
    }
}

internal sealed class FaultingPhotographyManagementDbContext(MuseumDbContext inner) : IMuseumDbContext
{
    public bool ThrowNextImageConcurrency { get; set; }
    public bool ThrowNextStateConcurrency { get; set; }
    public bool ThrowNextFirstStateCreateRace { get; set; }
    public Guid? CompetingPrimaryImageId { get; set; }
    public int ImageConcurrencyFailuresThrown { get; private set; }
    public int StateConcurrencyFailuresThrown { get; private set; }
    public int FirstStateCreateRaceFailuresThrown { get; private set; }
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
        if (ThrowNextImageConcurrency && inner.ChangeTracker.Entries<ArtifactImage>().Any(entry => entry.State == EntityState.Modified))
        {
            ThrowNextImageConcurrency = false;
            ImageConcurrencyFailuresThrown++;
            throw new DbUpdateConcurrencyException("Simulated artifact image concurrency failure.");
        }

        if (ThrowNextStateConcurrency && inner.ChangeTracker.Entries<ArtifactPhotographyState>().Any(entry => entry.State == EntityState.Modified))
        {
            ThrowNextStateConcurrency = false;
            StateConcurrencyFailuresThrown++;
            throw new DbUpdateConcurrencyException("Simulated artifact photography state concurrency failure.");
        }

        var addedState = inner.ChangeTracker.Entries<ArtifactPhotographyState>().FirstOrDefault(entry => entry.State == EntityState.Added);
        if (ThrowNextFirstStateCreateRace && addedState is not null)
        {
            ThrowNextFirstStateCreateRace = false;
            FirstStateCreateRaceFailuresThrown++;
            var artifactId = addedState.Entity.ArtifactId;
            inner.ChangeTracker.Clear();
            var competing = ArtifactPhotographyState.Create(artifactId);
            competing.SetPrimaryImage(CompetingPrimaryImageId ?? Guid.NewGuid(), "competing-manager");
            inner.ArtifactPhotographyStates.Add(competing);
            await inner.SaveChangesAsync(cancellationToken);
            throw new DbUpdateException("Simulated provider-neutral first primary state insert race.");
        }

        return await inner.SaveChangesAsync(cancellationToken);
    }
}

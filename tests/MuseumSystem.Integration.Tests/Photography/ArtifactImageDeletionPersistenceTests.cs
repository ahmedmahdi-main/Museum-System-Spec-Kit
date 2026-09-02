using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class ArtifactImageDeletionPersistenceTests(PostgresPhotographyTestFixture fixture)
{
    private static readonly DateTimeOffset DeletedAt = new(2026, 8, 25, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Available_image_roundtrips_with_no_deletion_metadata()
    {
        Guid imageId;
        await using (var seed = fixture.CreateContext())
        {
            var (artifactId, setId) = await SeedArtifactAndSetAsync(seed, "AR");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifactId, setId, UniqueObjectKey("available-roundtrip"));
            imageId = image.ArtifactImageId;
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);

        Assert.Equal(ArtifactImageStatus.Available, reloaded.Status);
        Assert.Null(reloaded.DeletedByUserId);
        Assert.Null(reloaded.DeletedAt);
        Assert.Null(reloaded.DeletionMode);
        Assert.Null(reloaded.DeletionReason);
    }

    [Fact]
    public async Task Delete_pending_lifecycle_roundtrips_with_mode_reason_and_concurrency_token()
    {
        Guid imageId;
        int expectedToken;
        await using (var seed = fixture.CreateContext())
        {
            var (artifactId, setId) = await SeedArtifactAndSetAsync(seed, "DP");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifactId, setId, UniqueObjectKey("delete-pending"));
            image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "  duplicate image  ");
            expectedToken = image.ConcurrencyToken;
            await seed.SaveChangesAsync();
            imageId = image.ArtifactImageId;
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);

        Assert.Equal(ArtifactImageStatus.DeletePending, reloaded.Status);
        Assert.Equal(ArtifactImageDeletionMode.Privileged, reloaded.DeletionMode);
        Assert.Equal("duplicate image", reloaded.DeletionReason);
        Assert.Equal(expectedToken, reloaded.ConcurrencyToken);
        Assert.Null(reloaded.DeletedByUserId);
        Assert.Null(reloaded.DeletedAt);
    }

    [Fact]
    public async Task Deleted_lifecycle_roundtrips_without_removing_the_image_row()
    {
        Guid imageId;
        int pendingToken;
        int deletedToken;
        await using (var seed = fixture.CreateContext())
        {
            var (artifactId, setId) = await SeedArtifactAndSetAsync(seed, "DR");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifactId, setId, UniqueObjectKey("deleted-roundtrip"));
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
            pendingToken = image.ConcurrencyToken;
            await seed.SaveChangesAsync();
            imageId = image.ArtifactImageId;
        }

        await using (var finalize = fixture.CreateContext())
        {
            var pending = await finalize.ArtifactImages.SingleAsync(image => image.ArtifactImageId == imageId);
            Assert.Equal(ArtifactImageStatus.DeletePending, pending.Status);
            Assert.Equal(pendingToken, pending.ConcurrencyToken);
            pending.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "uploader", DeletedAt);
            deletedToken = pending.ConcurrencyToken;
            await finalize.SaveChangesAsync();
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);

        Assert.Equal(ArtifactImageStatus.Deleted, reloaded.Status);
        Assert.Equal("uploader", reloaded.DeletedByUserId);
        Assert.Equal(DeletedAt, reloaded.DeletedAt);
        Assert.Equal(ArtifactImageDeletionMode.UploaderGracePeriod, reloaded.DeletionMode);
        Assert.Null(reloaded.DeletionReason);
        Assert.Equal(pendingToken + 1, deletedToken);
        Assert.Equal(deletedToken, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task Privileged_deletion_reason_roundtrips_as_normalized_metadata()
    {
        Guid imageId;
        await using (var seed = fixture.CreateContext())
        {
            var (artifactId, setId) = await SeedArtifactAndSetAsync(seed, "PR");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifactId, setId, UniqueObjectKey("privileged-reason"));
            image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "  curatorial duplicate  ");
            image.MarkDeleted(ArtifactImageDeletionMode.Privileged, "supervisor-1", DeletedAt);
            await seed.SaveChangesAsync();
            imageId = image.ArtifactImageId;
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);

        Assert.Equal(ArtifactImageStatus.Deleted, reloaded.Status);
        Assert.Equal(ArtifactImageDeletionMode.Privileged, reloaded.DeletionMode);
        Assert.Equal("curatorial duplicate", reloaded.DeletionReason);
    }

    [Fact]
    public async Task Uploader_grace_deletion_persists_successfully_without_manual_reason()
    {
        Guid imageId;
        await using (var seed = fixture.CreateContext())
        {
            var (artifactId, setId) = await SeedArtifactAndSetAsync(seed, "GR");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifactId, setId, UniqueObjectKey("grace-null-reason"));
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "uploader", DeletedAt);
            await seed.SaveChangesAsync();
            imageId = image.ArtifactImageId;
        }

        await using var reload = fixture.CreateContext();
        var reloaded = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);

        Assert.Equal(ArtifactImageStatus.Deleted, reloaded.Status);
        Assert.Equal(ArtifactImageDeletionMode.UploaderGracePeriod, reloaded.DeletionMode);
        Assert.Null(reloaded.DeletionReason);
    }

    [Fact]
    public async Task Audit_metadata_survives_after_image_is_marked_deleted()
    {
        Guid imageId;
        Guid auditId;
        await using (var seed = fixture.CreateContext())
        {
            var (artifactId, setId) = await SeedArtifactAndSetAsync(seed, "AU");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifactId, setId, UniqueObjectKey("audit-retention"));
            var audit = AuditEntry.Create(
                "supervisor-1",
                PhotographyAuditActions.ImageDeletePrivileged,
                "Photography",
                nameof(ArtifactImage),
                image.ArtifactImageId.ToString(),
                "Seeded deletion audit for persistence retention.",
                $"ArtifactId={artifactId}; ArtifactImageId={image.ArtifactImageId}; Mode=Privileged");
            seed.AuditEntries.Add(audit);
            image.MarkDeletePending(ArtifactImageDeletionMode.Privileged, "duplicate image");
            image.MarkDeleted(ArtifactImageDeletionMode.Privileged, "supervisor-1", DeletedAt);
            await seed.SaveChangesAsync();
            imageId = image.ArtifactImageId;
            auditId = audit.AuditEntryId;
        }

        await using var reload = fixture.CreateContext();
        var reloadedImage = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);
        var reloadedAudit = await reload.AuditEntries.AsNoTracking().SingleAsync(entry => entry.AuditEntryId == auditId);

        Assert.Equal(ArtifactImageStatus.Deleted, reloadedImage.Status);
        Assert.Equal(PhotographyAuditActions.ImageDeletePrivileged, reloadedAudit.ActionName);
        Assert.Equal(imageId.ToString(), reloadedAudit.EntityId);
    }

    [Fact]
    public async Task Deleting_current_primary_clears_primary_state_without_auto_replacement_and_advances_state_concurrency()
    {
        Guid artifactId;
        Guid imageAId;
        Guid imageBId;
        int stateTokenAfterSet;
        int stateTokenAfterClear;
        await using (var seed = fixture.CreateContext())
        {
            var (seedArtifactId, setId) = await SeedArtifactAndSetAsync(seed, "CP");
            var imageA = await PhotographyPersistenceTestData.SeedImageAsync(seed, seedArtifactId, setId, UniqueObjectKey("current-primary-a"));
            var imageB = await PhotographyPersistenceTestData.SeedImageAsync(seed, seedArtifactId, setId, UniqueObjectKey("current-primary-b"));
            var state = ArtifactPhotographyState.Create(seedArtifactId);
            state.SetPrimaryImage(imageA.ArtifactImageId, "manager-1");
            seed.ArtifactPhotographyStates.Add(state);
            await seed.SaveChangesAsync();
            artifactId = seedArtifactId;
            imageAId = imageA.ArtifactImageId;
            imageBId = imageB.ArtifactImageId;
            stateTokenAfterSet = state.ConcurrencyToken;
        }

        await using (var pendingContext = fixture.CreateContext())
        {
            var image = await pendingContext.ArtifactImages.SingleAsync(candidate => candidate.ArtifactImageId == imageAId);
            var state = await pendingContext.ArtifactPhotographyStates.SingleAsync(candidate => candidate.ArtifactId == artifactId);
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
            if (state.PrimaryImageId == image.ArtifactImageId)
            {
                state.ClearPrimaryImage("uploader");
            }

            stateTokenAfterClear = state.ConcurrencyToken;
            await pendingContext.SaveChangesAsync();
        }

        await using (var deleteContext = fixture.CreateContext())
        {
            var image = await deleteContext.ArtifactImages.SingleAsync(candidate => candidate.ArtifactImageId == imageAId);
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "uploader", DeletedAt);
            await deleteContext.SaveChangesAsync();
        }

        await using var reload = fixture.CreateContext();
        var reloadedState = await reload.ArtifactPhotographyStates.AsNoTracking().SingleAsync(state => state.ArtifactId == artifactId);
        var replacement = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageBId);

        Assert.Null(reloadedState.PrimaryImageId);
        Assert.NotEqual(imageBId, reloadedState.PrimaryImageId);
        Assert.Equal(ArtifactImageStatus.Available, replacement.Status);
        Assert.Equal(stateTokenAfterSet + 1, stateTokenAfterClear);
        Assert.Equal(stateTokenAfterClear, reloadedState.ConcurrencyToken);
    }

    [Fact]
    public async Task Deleting_non_primary_image_leaves_current_primary_unchanged()
    {
        Guid artifactId;
        Guid primaryImageId;
        Guid deletedImageId;
        await using (var seed = fixture.CreateContext())
        {
            var (seedArtifactId, setId) = await SeedArtifactAndSetAsync(seed, "NP");
            var primary = await PhotographyPersistenceTestData.SeedImageAsync(seed, seedArtifactId, setId, UniqueObjectKey("non-primary-a"));
            var other = await PhotographyPersistenceTestData.SeedImageAsync(seed, seedArtifactId, setId, UniqueObjectKey("non-primary-b"));
            var state = ArtifactPhotographyState.Create(seedArtifactId);
            state.SetPrimaryImage(primary.ArtifactImageId, "manager-1");
            seed.ArtifactPhotographyStates.Add(state);
            await seed.SaveChangesAsync();
            artifactId = seedArtifactId;
            primaryImageId = primary.ArtifactImageId;
            deletedImageId = other.ArtifactImageId;
        }

        await using (var deleteContext = fixture.CreateContext())
        {
            var image = await deleteContext.ArtifactImages.SingleAsync(candidate => candidate.ArtifactImageId == deletedImageId);
            var state = await deleteContext.ArtifactPhotographyStates.SingleAsync(candidate => candidate.ArtifactId == artifactId);
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
            if (state.PrimaryImageId == image.ArtifactImageId)
            {
                state.ClearPrimaryImage("uploader");
            }

            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "uploader", DeletedAt);
            await deleteContext.SaveChangesAsync();
        }

        await using var reload = fixture.CreateContext();
        var reloadedState = await reload.ArtifactPhotographyStates.AsNoTracking().SingleAsync(state => state.ArtifactId == artifactId);
        var reloadedPrimary = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == primaryImageId);

        Assert.Equal(primaryImageId, reloadedState.PrimaryImageId);
        Assert.Equal(ArtifactImageStatus.Available, reloadedPrimary.Status);
    }

    [Fact]
    public async Task Deleted_image_after_fresh_reload_is_not_primary_eligible()
    {
        Guid artifactId;
        Guid imageId;
        await using (var seed = fixture.CreateContext())
        {
            var (seedArtifactId, setId) = await SeedArtifactAndSetAsync(seed, "IE");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, seedArtifactId, setId, UniqueObjectKey("ineligible-after-reload"));
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "uploader", DeletedAt);
            await seed.SaveChangesAsync();
            artifactId = seedArtifactId;
            imageId = image.ArtifactImageId;
        }

        await using var reload = fixture.CreateContext();
        var deletedImage = await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId);

        Assert.False(PhotographyRules.IsPrimaryImageEligible(deletedImage, artifactId));
    }

    [Fact]
    public async Task Derivative_database_metadata_rows_are_retained_during_deletion_foundation_checkpoint()
    {
        Guid imageId;
        Guid thumbnailId;
        Guid previewId;
        await using (var seed = fixture.CreateContext())
        {
            var (artifactId, setId) = await SeedArtifactAndSetAsync(seed, "DM");
            var image = await PhotographyPersistenceTestData.SeedImageAsync(seed, artifactId, setId, UniqueObjectKey("derivative-retention"));
            var thumbnail = ArtifactImageDerivative.Create(image.ArtifactImageId, ImageDerivativeKind.Thumbnail, ImageStorageObjectKey.Create(UniqueObjectKey("thumbnail-retention")), "image/jpeg", 128, 120, 90);
            var preview = ArtifactImageDerivative.Create(image.ArtifactImageId, ImageDerivativeKind.Preview, ImageStorageObjectKey.Create(UniqueObjectKey("preview-retention")), "image/jpeg", 512, 640, 480);
            seed.ArtifactImageDerivatives.AddRange(thumbnail, preview);
            await seed.SaveChangesAsync();
            image.MarkDeletePending(ArtifactImageDeletionMode.UploaderGracePeriod);
            image.MarkDeleted(ArtifactImageDeletionMode.UploaderGracePeriod, "uploader", DeletedAt);
            await seed.SaveChangesAsync();
            imageId = image.ArtifactImageId;
            thumbnailId = thumbnail.ArtifactImageDerivativeId;
            previewId = preview.ArtifactImageDerivativeId;
        }

        await using var reload = fixture.CreateContext();
        Assert.Equal(ArtifactImageStatus.Deleted, (await reload.ArtifactImages.AsNoTracking().SingleAsync(image => image.ArtifactImageId == imageId)).Status);
        Assert.NotNull(await reload.ArtifactImageDerivatives.AsNoTracking().SingleOrDefaultAsync(derivative => derivative.ArtifactImageDerivativeId == thumbnailId));
        Assert.NotNull(await reload.ArtifactImageDerivatives.AsNoTracking().SingleOrDefaultAsync(derivative => derivative.ArtifactImageDerivativeId == previewId));
    }

    private static async Task<(Guid ArtifactId, Guid SetId)> SeedArtifactAndSetAsync(MuseumSystem.Infrastructure.Persistence.MuseumDbContext context, string prefix)
    {
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, prefix);
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, artifact.ArtifactId);
        return (artifact.ArtifactId, set.PhotographySetId);
    }

    private static string UniqueObjectKey(string name) => $"original/{name}-{Guid.NewGuid():N}";
}


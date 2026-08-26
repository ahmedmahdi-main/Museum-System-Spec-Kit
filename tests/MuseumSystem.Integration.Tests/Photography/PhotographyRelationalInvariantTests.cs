using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Photography;
using Npgsql;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyRelationalInvariantTests(PostgresPhotographyTestFixture fixture)
{
    [Fact]
    public async Task Photography_set_requires_existing_artifact()
    {
        await using var context = fixture.CreateContext();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographySets" (
                "PhotographySetId", "ArtifactId", "Purpose", "PhotographyDate", "PhotographerUserId", "CreatedAt", "ConcurrencyToken")
            values ({0}, {1}, 'GeneralDocumentation', DATE '2026-08-24', 'photographer', now(), 0)
            """, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Artifact_image_requires_existing_artifact()
    {
        await using var context = fixture.CreateContext();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactImages" (
                "ArtifactImageId", "ArtifactId", "PhotographySetId", "OriginalObjectKey", "OriginalFilename", "ContentType",
                "FileSizeBytes", "PixelWidth", "PixelHeight", "UploadedByUserId", "UploadedAt", "Status", "ConcurrencyToken")
            values ({0}, {1}, {2}, 'original/missing-artifact', 'missing.png', 'image/png', 1, 1, 1, 'uploader', now(), 'Available', 0)
            """, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Artifact_image_must_reference_photography_set_for_same_artifact()
    {
        await using var context = fixture.CreateContext();
        var firstArtifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "IM");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, firstArtifact.ArtifactId);
        var secondArtifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "IN");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactImages" (
                "ArtifactImageId", "ArtifactId", "PhotographySetId", "OriginalObjectKey", "OriginalFilename", "ContentType",
                "FileSizeBytes", "PixelWidth", "PixelHeight", "UploadedByUserId", "UploadedAt", "Status", "ConcurrencyToken")
            values ({0}, {1}, {2}, 'original/mismatched-set', 'mismatch.png', 'image/png', 1, 1, 1, 'uploader', now(), 'Available', 0)
            """, Guid.NewGuid(), secondArtifact.ArtifactId, set.PhotographySetId));
    }

    [Fact]
    public async Task Original_object_keys_are_unique()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "OU");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, artifact.ArtifactId);
        await PhotographyPersistenceTestData.SeedImageAsync(context, artifact.ArtifactId, set.PhotographySetId, "original/unique-key");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactImages" (
                "ArtifactImageId", "ArtifactId", "PhotographySetId", "OriginalObjectKey", "OriginalFilename", "ContentType",
                "FileSizeBytes", "PixelWidth", "PixelHeight", "UploadedByUserId", "UploadedAt", "Status", "ConcurrencyToken")
            values ({0}, {1}, {2}, 'original/unique-key', 'duplicate.png', 'image/png', 1, 1, 1, 'uploader', now(), 'Available', 0)
            """, Guid.NewGuid(), artifact.ArtifactId, set.PhotographySetId));
    }

    [Fact]
    public async Task Derivative_object_keys_are_unique()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "DU");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, artifact.ArtifactId);
        var first = await PhotographyPersistenceTestData.SeedImageAsync(context, artifact.ArtifactId, set.PhotographySetId, "original/derivative-1");
        var second = await PhotographyPersistenceTestData.SeedImageAsync(context, artifact.ArtifactId, set.PhotographySetId, "original/derivative-2");

        context.ArtifactImageDerivatives.Add(ArtifactImageDerivative.Create(first.ArtifactImageId, ImageDerivativeKind.Thumbnail, ImageStorageObjectKey.Create("derivative/shared"), "image/jpeg", 128, 64, 64));
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactImageDerivatives" (
                "ArtifactImageDerivativeId", "ArtifactImageId", "Kind", "ObjectKey", "ContentType", "FileSizeBytes", "PixelWidth", "PixelHeight", "CreatedAt")
            values ({0}, {1}, 'Thumbnail', 'derivative/shared', 'image/jpeg', 128, 64, 64, now())
            """, Guid.NewGuid(), second.ArtifactImageId));
    }

    [Fact]
    public async Task Artifact_photography_state_primary_image_must_belong_to_same_artifact()
    {
        await using var context = fixture.CreateContext();
        var firstArtifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "PI");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, firstArtifact.ArtifactId);
        var image = await PhotographyPersistenceTestData.SeedImageAsync(context, firstArtifact.ArtifactId, set.PhotographySetId, "original/primary-other-artifact");
        var secondArtifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "PJ");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."ArtifactPhotographyStates" (
                "ArtifactId", "PrimaryImageId", "ConcurrencyToken", "UpdatedAt", "UpdatedByUserId")
            values ({0}, {1}, 0, now(), 'manager')
            """, secondArtifact.ArtifactId, image.ArtifactImageId));
    }

    [Fact]
    public async Task Upload_operation_idempotency_key_is_unique_per_actor_and_operation_kind()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "UO");

        context.PhotographyUploadOperations.Add(PhotographyUploadOperation.Start("actor", PhotographyUploadOperationKind.CreateSetUpload, "idem", "fingerprint-1", artifact.ArtifactId));
        context.PhotographyUploadOperations.Add(PhotographyUploadOperation.Start("actor", PhotographyUploadOperationKind.CreateSetUpload, "idem", "fingerprint-2", artifact.ArtifactId));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Upload_file_ordinal_is_unique_per_operation()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "OF");
        var operation = PhotographyUploadOperation.Start("actor", PhotographyUploadOperationKind.CreateSetUpload, "idem-ordinal", "fingerprint", artifact.ArtifactId);
        context.PhotographyUploadOperations.Add(operation);
        await context.SaveChangesAsync();

        context.PhotographyUploadFileOutcomes.Add(PhotographyUploadFileOutcome.Rejected(operation.PhotographyUploadOperationId, 0, "a.txt", "fingerprint-a", "Rejected."));
        context.PhotographyUploadFileOutcomes.Add(PhotographyUploadFileOutcome.Rejected(operation.PhotographyUploadOperationId, 0, "b.txt", "fingerprint-b", "Rejected."));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Same_upload_file_ordinal_is_allowed_for_different_operations()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "OA");
        var first = PhotographyUploadOperation.Start("actor", PhotographyUploadOperationKind.CreateSetUpload, "idem-a", "fingerprint-a", artifact.ArtifactId);
        var second = PhotographyUploadOperation.Start("actor", PhotographyUploadOperationKind.CreateSetUpload, "idem-b", "fingerprint-b", artifact.ArtifactId);
        context.PhotographyUploadOperations.AddRange(first, second);
        await context.SaveChangesAsync();

        context.PhotographyUploadFileOutcomes.Add(PhotographyUploadFileOutcome.Rejected(first.PhotographyUploadOperationId, 0, "a.txt", "file-a", "Rejected."));
        context.PhotographyUploadFileOutcomes.Add(PhotographyUploadFileOutcome.Rejected(second.PhotographyUploadOperationId, 0, "b.txt", "file-b", "Rejected."));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.PhotographyUploadFileOutcomes.CountAsync());
    }

    [Theory]
    [InlineData("""
        insert into museum."PhotographySets" (
            "PhotographySetId", "ArtifactId", "Purpose", "PhotographyDate", "PhotographerUserId", "CreatedAt", "ConcurrencyToken")
        values ({0}, {1}, 'Exhibition', DATE '2026-08-24', 'photographer', now(), 0)
        """)]
    [InlineData("""
        insert into museum."PhotographyUploadOperations" (
            "PhotographyUploadOperationId", "ActorUserId", "OperationKind", "IdempotencyKey", "RequestFingerprint",
            "ArtifactId", "Status", "StartedAt", "LastSeenAt", "ConcurrencyToken")
        values ({0}, 'actor', 'Unsupported', 'idem-invalid', 'fingerprint', {1}, 'InProgress', now(), now(), 0)
        """)]
    [InlineData("""
        insert into museum."StorageOperationRecoveries" (
            "StorageOperationRecoveryId", "OperationType", "ArtifactId", "ObjectKeys", "Status", "FailureSummary", "CreatedAt", "ConcurrencyToken")
        values ({0}, 'UploadCleanup', {1}, '[""object/key""]'::jsonb, 'Unknown', 'failure', now(), 0)
        """)]
    public async Task Invalid_persisted_finite_state_values_are_rejected(string sql)
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "CK");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync(sql, Guid.NewGuid(), artifact.ArtifactId));
    }
}

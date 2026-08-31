using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.Photography;
using Npgsql;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyRequestPersistenceTests(PostgresPhotographyTestFixture fixture)
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 8, 24, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = new(2026, 8, 24, 11, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CancelledAt = new(2026, 8, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Photography_request_schema_exists_through_migrations()
    {
        await using var context = fixture.CreateContext();

        var migration = await context.Database.SqlQueryRaw<string>("""
            select "MigrationId" as "Value" from "__EFMigrationsHistory"
            where "MigrationId" = '20260824000200_AddPhotographyRequestSchema'
            """).SingleAsync();
        var tableCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value" from information_schema.tables
            where table_schema = 'museum' and table_name = 'PhotographyRequests'
            """).SingleAsync();

        Assert.Equal("20260824000200_AddPhotographyRequestSchema", migration);
        Assert.Equal(1, tableCount);
    }

    [Fact]
    public async Task Pending_request_persists_and_round_trips_without_artifact_snapshot_data()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RQ");
        var request = PhotographyRequest.Create(
            artifact.ArtifactId,
            PhotographyPurpose.PreMaintenance,
            "requester-1",
            RequestedAt);

        context.PhotographyRequests.Add(request);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.PhotographyRequests.SingleAsync(row => row.PhotographyRequestId == request.PhotographyRequestId);
        var artifactDataColumnCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value"
            from information_schema.columns
            where table_schema = 'museum'
              and table_name = 'PhotographyRequests'
              and column_name in ('MuseumNumberDisplay', 'MuseumNumber', 'CategoryId', 'BasicDescription', 'CurrentLocationId', 'CurrentHolderName', 'CurrentStatus')
            """).SingleAsync();

        Assert.Equal(artifact.ArtifactId, reloaded.ArtifactId);
        Assert.Equal(PhotographyPurpose.PreMaintenance, reloaded.Purpose);
        Assert.Equal("requester-1", reloaded.RequestedByUserId);
        Assert.Equal(RequestedAt, reloaded.RequestedAt);
        Assert.Equal(PhotographyRequestStatus.Pending, reloaded.Status);
        Assert.Null(reloaded.FulfillingPhotographySetId);
        Assert.Null(reloaded.CompletedByUserId);
        Assert.Null(reloaded.CompletedAt);
        Assert.Null(reloaded.CancelledByUserId);
        Assert.Null(reloaded.CancelledAt);
        Assert.Equal(0, artifactDataColumnCount);
    }

    [Fact]
    public async Task Request_requires_existing_artifact()
    {
        await using var context = fixture.CreateContext();

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographyRequests" (
                "PhotographyRequestId", "ArtifactId", "Purpose", "RequestedByUserId", "RequestedAt", "Status", "ConcurrencyToken")
            values ({0}, {1}, 'GeneralDocumentation', 'requester', now(), 'Pending', 0)
            """, Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task Invalid_request_status_is_rejected_by_schema()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RS");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographyRequests" (
                "PhotographyRequestId", "ArtifactId", "Purpose", "RequestedByUserId", "RequestedAt", "Status", "ConcurrencyToken")
            values ({0}, {1}, 'GeneralDocumentation', 'requester', now(), 'InProgress', 0)
            """, Guid.NewGuid(), artifact.ArtifactId));
    }

    [Fact]
    public async Task Completed_request_requires_fulfilling_set_and_completion_metadata()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RC");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographyRequests" (
                "PhotographyRequestId", "ArtifactId", "Purpose", "RequestedByUserId", "RequestedAt", "Status",
                "CompletedByUserId", "CompletedAt", "ConcurrencyToken")
            values ({0}, {1}, 'GeneralDocumentation', 'requester', now(), 'Completed', 'photographer', now(), 0)
            """, Guid.NewGuid(), artifact.ArtifactId));
    }

    [Fact]
    public async Task Completed_request_metadata_round_trips_with_valid_available_image_fulfillment_fact()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RF");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, artifact.ArtifactId);
        await PhotographyPersistenceTestData.SeedImageAsync(context, artifact.ArtifactId, set.PhotographySetId, $"original/request-fulfillment-{Guid.NewGuid():N}");
        var request = PhotographyRequest.Create(
            artifact.ArtifactId,
            set.Purpose,
            "requester-1",
            RequestedAt);

        request.Complete(
            set.PhotographySetId,
            set.ArtifactId,
            set.Purpose,
            fulfillingSetHasAvailableImage: true,
            "photographer-1",
            CompletedAt);
        context.PhotographyRequests.Add(request);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.PhotographyRequests.SingleAsync(row => row.PhotographyRequestId == request.PhotographyRequestId);
        Assert.Equal(PhotographyRequestStatus.Completed, reloaded.Status);
        Assert.Equal(set.PhotographySetId, reloaded.FulfillingPhotographySetId);
        Assert.Equal("photographer-1", reloaded.CompletedByUserId);
        Assert.Equal(CompletedAt, reloaded.CompletedAt);
        Assert.Null(reloaded.CancelledByUserId);
        Assert.Null(reloaded.CancelledAt);
        Assert.Equal(1, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task Cancellation_metadata_round_trips_and_terminal_state_persists_after_reload()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RN");
        var request = PhotographyRequest.Create(
            artifact.ArtifactId,
            PhotographyPurpose.GeneralDocumentation,
            "requester-1",
            RequestedAt);
        request.Cancel("manager-1", CancelledAt, actorHasManageAuthority: true);

        context.PhotographyRequests.Add(request);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var reloaded = await context.PhotographyRequests.SingleAsync(row => row.PhotographyRequestId == request.PhotographyRequestId);
        Assert.Equal(PhotographyRequestStatus.Cancelled, reloaded.Status);
        Assert.Equal("manager-1", reloaded.CancelledByUserId);
        Assert.Equal(CancelledAt, reloaded.CancelledAt);
        Assert.Null(reloaded.FulfillingPhotographySetId);
        Assert.Null(reloaded.CompletedByUserId);
        Assert.Null(reloaded.CompletedAt);
        Assert.Equal(1, reloaded.ConcurrencyToken);
    }

    [Fact]
    public async Task Many_completed_requests_may_reference_the_same_fulfilling_set()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RM");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, artifact.ArtifactId);
        await PhotographyPersistenceTestData.SeedImageAsync(context, artifact.ArtifactId, set.PhotographySetId, $"original/request-many-{Guid.NewGuid():N}");
        var first = PhotographyRequest.Create(artifact.ArtifactId, set.Purpose, "requester-1", RequestedAt);
        var second = PhotographyRequest.Create(artifact.ArtifactId, set.Purpose, "requester-2", RequestedAt.AddMinutes(1));

        first.Complete(set.PhotographySetId, set.ArtifactId, set.Purpose, true, "photographer-1", CompletedAt);
        second.Complete(set.PhotographySetId, set.ArtifactId, set.Purpose, true, "photographer-1", CompletedAt.AddMinutes(1));
        context.PhotographyRequests.AddRange(first, second);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var fulfilledCount = await context.PhotographyRequests
            .CountAsync(request => request.FulfillingPhotographySetId == set.PhotographySetId);

        Assert.Equal(2, fulfilledCount);
    }

    [Fact]
    public async Task Completed_request_fulfilling_set_must_belong_to_same_artifact()
    {
        await using var context = fixture.CreateContext();
        var firstArtifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RA");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, firstArtifact.ArtifactId);
        var secondArtifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RB");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographyRequests" (
                "PhotographyRequestId", "ArtifactId", "Purpose", "RequestedByUserId", "RequestedAt", "Status",
                "FulfillingPhotographySetId", "CompletedByUserId", "CompletedAt", "ConcurrencyToken")
            values ({0}, {1}, 'GeneralDocumentation', 'requester', now(), 'Completed', {2}, 'photographer', now(), 0)
            """, Guid.NewGuid(), secondArtifact.ArtifactId, set.PhotographySetId));
    }

    [Fact]
    public async Task Invalid_request_purpose_is_rejected_by_schema()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RP");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographyRequests" (
                "PhotographyRequestId", "ArtifactId", "Purpose", "RequestedByUserId", "RequestedAt", "Status", "ConcurrencyToken")
            values ({0}, {1}, 'Exhibition', 'requester', now(), 'Pending', 0)
            """, Guid.NewGuid(), artifact.ArtifactId));
    }

    [Fact]
    public async Task Cancelled_request_requires_cancellation_metadata()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RX");

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographyRequests" (
                "PhotographyRequestId", "ArtifactId", "Purpose", "RequestedByUserId", "RequestedAt", "Status", "ConcurrencyToken")
            values ({0}, {1}, 'GeneralDocumentation', 'requester', now(), 'Cancelled', 0)
            """, Guid.NewGuid(), artifact.ArtifactId));
    }

    [Fact]
    public async Task Completed_request_fulfilling_set_must_have_same_purpose()
    {
        await using var context = fixture.CreateContext();
        var artifact = await PhotographyPersistenceTestData.SeedArtifactAsync(context, "RY");
        var set = await PhotographyPersistenceTestData.SeedSetAsync(context, artifact.ArtifactId);

        await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlRawAsync("""
            insert into museum."PhotographyRequests" (
                "PhotographyRequestId", "ArtifactId", "Purpose", "RequestedByUserId", "RequestedAt", "Status",
                "FulfillingPhotographySetId", "CompletedByUserId", "CompletedAt", "ConcurrencyToken")
            values ({0}, {1}, 'PostMaintenance', 'requester', now(), 'Completed', {2}, 'photographer', now(), 0)
            """, Guid.NewGuid(), artifact.ArtifactId, set.PhotographySetId));
    }

    [Fact]
    public async Task Request_schema_contains_useful_indexes_and_constraints()
    {
        await using var context = fixture.CreateContext();

        var requiredIndexCount = await context.Database.SqlQueryRaw<int>("""
            with expected(indexed_columns) as (
                values
                    (array['ArtifactId']::text[]),
                    (array['ArtifactId', 'Status']::text[]),
                    (array['RequestedByUserId']::text[]),
                    (array['FulfillingPhotographySetId', 'ArtifactId', 'Purpose']::text[])
            )
            select count(*)::int as "Value"
            from expected e
            where exists (
                select 1
                from pg_class table_class
                join pg_namespace table_schema on table_schema.oid = table_class.relnamespace
                join pg_index index_metadata on index_metadata.indrelid = table_class.oid
                join lateral (
                    select array_agg(index_attribute.attname::text order by index_key.ordinality) as columns
                    from unnest(index_metadata.indkey) with ordinality as index_key(attnum, ordinality)
                    join pg_attribute index_attribute
                      on index_attribute.attrelid = table_class.oid
                     and index_attribute.attnum = index_key.attnum
                ) actual on true
                where table_schema.nspname = 'museum'
                  and table_class.relname = 'PhotographyRequests'
                  and actual.columns = e.indexed_columns
            )
            """).SingleAsync();
        var requiredConstraintCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value"
            from pg_constraint c
            join pg_namespace n on n.oid = c.connamespace
            where n.nspname = 'museum'
              and conname in (
                'CK_PhotographyRequests_Purpose',
                'CK_PhotographyRequests_Status',
                'CK_PhotographyRequests_CompletedMetadata',
                'CK_PhotographyRequests_CancelledMetadata',
                'FK_PhotographyRequests_Artifacts_ArtifactId',
                'FK_PhotographyRequests_PhotographySets_FulfillingPhotographySe~',
                'AK_PhotographySets_PhotographySetId_ArtifactId_Purpose')
            """).SingleAsync();

        Assert.Equal(4, requiredIndexCount);
        Assert.Equal(7, requiredConstraintCount);
    }
}

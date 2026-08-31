using Microsoft.EntityFrameworkCore;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyMigrationTests(PostgresPhotographyTestFixture fixture)
{
    [Fact]
    public async Task Photography_core_and_request_schema_are_applied()
    {
        await using var context = fixture.CreateContext();

        var migration = await context.Database.SqlQueryRaw<string>("""
            select "MigrationId" as "Value" from "__EFMigrationsHistory"
            where "MigrationId" = '20260824000100_AddPhotographyCoreSchema'
            """).SingleAsync();
        var coreTableCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value" from information_schema.tables
            where table_schema = 'museum' and table_name in (
                'PhotographySets',
                'ArtifactImages',
                'ArtifactImageDerivatives',
                'ArtifactPhotographyStates',
                'PhotographyUploadOperations',
                'PhotographyUploadFileOutcomes',
                'StorageOperationRecoveries')
            """).SingleAsync();
        var requestTableCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value" from information_schema.tables
            where table_schema = 'museum' and table_name = 'PhotographyRequests'
            """).SingleAsync();

        Assert.Equal("20260824000100_AddPhotographyCoreSchema", migration);
        Assert.Equal(7, coreTableCount);
        Assert.Equal(1, requestTableCount);
    }

    [Fact]
    public async Task Photography_schema_contains_required_indexes_foreign_keys_and_check_constraints()
    {
        await using var context = fixture.CreateContext();

        var namedConstraints = await context.Database.SqlQueryRaw<string>("""
            select conname as "Value"
            from pg_constraint c
            join pg_namespace n on n.oid = c.connamespace
            where n.nspname = 'museum'
              and conname in (
                'AK_PhotographySets_PhotographySetId_ArtifactId',
                'AK_ArtifactImages_ArtifactImageId_ArtifactId',
                'CK_PhotographySets_Purpose',
                'CK_ArtifactImages_Status',
                'CK_ArtifactImages_DeletionMode',
                'CK_ArtifactImages_FileSizeBytes',
                'CK_ArtifactImages_PixelWidth',
                'CK_ArtifactImages_PixelHeight',
                'CK_ArtifactImageDerivatives_Kind',
                'CK_ArtifactImageDerivatives_FileSizeBytes',
                'CK_ArtifactImageDerivatives_PixelWidth',
                'CK_ArtifactImageDerivatives_PixelHeight',
                'CK_PhotographyUploadOperations_OperationKind',
                'CK_PhotographyUploadOperations_Status',
                'CK_PhotographyUploadFileOutcomes_ClientFileOrdinal',
                'CK_PhotographyUploadFileOutcomes_Status',
                'CK_StorageOperationRecoveries_OperationType',
                'CK_StorageOperationRecoveries_Status')
            """).ToListAsync();
        var requiredUniqueIndexCount = await context.Database.SqlQueryRaw<int>("""
            with expected(table_name, indexed_columns) as (
                values
                    ('ArtifactImages', array['OriginalObjectKey']::text[]),
                    ('ArtifactImageDerivatives', array['ObjectKey']::text[]),
                    ('PhotographyUploadOperations', array['ActorUserId', 'OperationKind', 'IdempotencyKey']::text[]),
                    ('PhotographyUploadFileOutcomes', array['PhotographyUploadOperationId', 'ClientFileOrdinal']::text[]),
                    ('PhotographyUploadFileOutcomes', array['OriginalObjectKey']::text[])
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
                  and table_class.relname = e.table_name
                  and index_metadata.indisunique
                  and actual.columns = e.indexed_columns
            )
            """).SingleAsync();
        var derivativeKindUniqueIndexCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value"
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
              and table_class.relname = 'ArtifactImageDerivatives'
              and index_metadata.indisunique
              and actual.columns = array['ArtifactImageId', 'Kind']::text[]
            """).SingleAsync();
        var requiredForeignKeyCount = await context.Database.SqlQueryRaw<int>("""
            with expected(source_table, source_columns, target_table, target_columns) as (
                values
                    ('ArtifactImages', array['ArtifactId']::text[], 'Artifacts', array['ArtifactId']::text[]),
                    ('ArtifactImages', array['PhotographySetId', 'ArtifactId']::text[], 'PhotographySets', array['PhotographySetId', 'ArtifactId']::text[]),
                    ('ArtifactPhotographyStates', array['PrimaryImageId', 'ArtifactId']::text[], 'ArtifactImages', array['ArtifactImageId', 'ArtifactId']::text[])
            )
            select count(*)::int as "Value"
            from expected e
            where exists (
                select 1
                from pg_constraint constraint_metadata
                join pg_class source_table_class on source_table_class.oid = constraint_metadata.conrelid
                join pg_namespace source_schema on source_schema.oid = source_table_class.relnamespace
                join pg_class target_table_class on target_table_class.oid = constraint_metadata.confrelid
                join pg_namespace target_schema on target_schema.oid = target_table_class.relnamespace
                join lateral (
                    select array_agg(source_attribute.attname::text order by source_key.ordinality) as columns
                    from unnest(constraint_metadata.conkey) with ordinality as source_key(attnum, ordinality)
                    join pg_attribute source_attribute
                      on source_attribute.attrelid = source_table_class.oid
                     and source_attribute.attnum = source_key.attnum
                ) source_columns on true
                join lateral (
                    select array_agg(target_attribute.attname::text order by target_key.ordinality) as columns
                    from unnest(constraint_metadata.confkey) with ordinality as target_key(attnum, ordinality)
                    join pg_attribute target_attribute
                      on target_attribute.attrelid = target_table_class.oid
                     and target_attribute.attnum = target_key.attnum
                ) target_columns on true
                where constraint_metadata.contype = 'f'
                  and source_schema.nspname = 'museum'
                  and target_schema.nspname = 'museum'
                  and source_table_class.relname = e.source_table
                  and target_table_class.relname = e.target_table
                  and source_columns.columns = e.source_columns
                  and target_columns.columns = e.target_columns
            )
            """).SingleAsync();
        Assert.Equal(18, namedConstraints.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(5, requiredUniqueIndexCount);
        Assert.Equal(0, derivativeKindUniqueIndexCount);
        Assert.Equal(3, requiredForeignKeyCount);
    }
}

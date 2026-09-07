using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.Photography;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Integration.Tests.Photography;

[Collection(PostgresPhotographyCollection.Name)]
public sealed class PhotographyDocumentationBoundaryRegressionTests(PostgresPhotographyTestFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset RequestedAt = new(2026, 8, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset UploadedAt = new(2026, 8, 28, 10, 0, 0, TimeSpan.Zero);
    private readonly List<Guid> createdArtifactIds = [];
    private readonly List<Guid> createdCategoryIds = [];
    private readonly List<Guid> createdLocationIds = [];
    private readonly List<Guid> createdTemplateIds = [];

    [Fact]
    public async Task Photography_set_preserves_existing_documentation_record_values_and_status()
    {
        var seed = await SeedDocumentationWithCompletedCorrectionAsync("DR");

        DocumentationBoundarySnapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadDocumentationSnapshotAsync(beforeContext, seed.DocumentationRecordId);
        }

        var (setId, _) = await PersistPhotographySetWithImageAsync(seed.ArtifactId, "documentation-record-boundary");

        DocumentationBoundarySnapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadDocumentationSnapshotAsync(afterContext, seed.DocumentationRecordId);
            var set = await afterContext.PhotographySets.AsNoTracking().SingleAsync(candidate => candidate.PhotographySetId == setId);

            Assert.Equal(seed.ArtifactId, set.ArtifactId);
            Assert.Equal(after.Record.ArtifactId, set.ArtifactId);
        }

        AssertDocumentationSnapshotUnchanged(before, after);
        Assert.Equal(DocumentationRecordStatus.Completed, after.Record.Status);
        AssertJsonEqual(before.Record.ValuesJson, after.Record.ValuesJson);
        AssertJsonEqual(before.Record.CompletedBaselineValuesJson, after.Record.CompletedBaselineValuesJson);
    }

    [Fact]
    public async Task Photography_set_preserves_documentation_template_version_fields_and_options()
    {
        var seed = await SeedDocumentationWithCompletedCorrectionAsync("TV");

        DocumentationBoundarySnapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadDocumentationSnapshotAsync(beforeContext, seed.DocumentationRecordId);
        }

        await PersistPhotographySetWithImageAsync(seed.ArtifactId, "template-boundary");

        DocumentationBoundarySnapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadDocumentationSnapshotAsync(afterContext, seed.DocumentationRecordId);
        }

        AssertTemplateSnapshotUnchanged(before.Template, after.Template);
        Assert.Equal(DocumentationTemplateVersionStatus.Active, after.Template.Version.Status);
        Assert.Equal(4, after.Template.Fields.Count);
        Assert.Contains(after.Template.Fields, field => field.FieldKey == "condition" && field.Options.Count == 2);
    }

    [Fact]
    public async Task Photography_set_preserves_completed_documentation_revision_history()
    {
        var seed = await SeedDocumentationWithCompletedCorrectionAsync("RV");

        DocumentationBoundarySnapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadDocumentationSnapshotAsync(beforeContext, seed.DocumentationRecordId);
        }

        await PersistPhotographySetWithImageAsync(seed.ArtifactId, "revision-boundary");

        DocumentationBoundarySnapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadDocumentationSnapshotAsync(afterContext, seed.DocumentationRecordId);
        }

        Assert.Single(after.Revisions);
        AssertDocumentationSnapshotUnchanged(before, after);
        Assert.Equal(2, after.Revisions.Single().RevisionNumber);
        AssertJsonEqual(before.Revisions.Single().PreviousValuesJson, after.Revisions.Single().PreviousValuesJson);
        AssertJsonEqual(before.Revisions.Single().NewValuesJson, after.Revisions.Single().NewValuesJson);
        AssertJsonEqual(before.Revisions.Single().ChangeSummaryJson, after.Revisions.Single().ChangeSummaryJson);
    }

    [Fact]
    public async Task Photography_request_references_same_artifact_without_creating_or_changing_documentation_work()
    {
        var seed = await SeedDocumentationWithCompletedCorrectionAsync("RQ");

        DocumentationBoundarySnapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadDocumentationSnapshotAsync(beforeContext, seed.DocumentationRecordId);
        }

        Guid requestId;
        await using (var actionContext = fixture.CreateContext())
        {
            var useCase = new CreatePhotographyRequestUseCase(
                actionContext,
                new BoundaryAuditActorContext("requester-1"),
                new StaticPermissionChecker([PermissionNames.PhotographyRequest]),
                new RecordingAuditWriter(),
                new FixedTimeProvider(RequestedAt));

            var result = await useCase.CreatePhotographyRequest(new CreatePhotographyRequestCommand(
                seed.ArtifactId,
                PhotographyPurpose.PreMaintenance));

            Assert.True(result.Succeeded);
            requestId = result.Value!.PhotographyRequestId;
        }

        DocumentationBoundarySnapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadDocumentationSnapshotAsync(afterContext, seed.DocumentationRecordId);
            var request = await afterContext.PhotographyRequests.AsNoTracking().SingleAsync(candidate => candidate.PhotographyRequestId == requestId);

            Assert.Equal(seed.ArtifactId, request.ArtifactId);
            Assert.Equal(after.Record.ArtifactId, request.ArtifactId);
            Assert.Equal(1, await afterContext.DocumentationRecords.CountAsync(record => record.ArtifactId == seed.ArtifactId));
        }

        AssertDocumentationSnapshotUnchanged(before, after);
    }

    [Fact]
    public async Task Setting_primary_image_preserves_documentation_record_template_and_revision_state()
    {
        var seed = await SeedDocumentationWithCompletedCorrectionAsync("PI");
        var (_, imageId) = await PersistPhotographySetWithImageAsync(seed.ArtifactId, "primary-documentation-boundary");

        DocumentationBoundarySnapshot before;
        await using (var beforeContext = fixture.CreateContext())
        {
            before = await LoadDocumentationSnapshotAsync(beforeContext, seed.DocumentationRecordId);
        }

        await using (var actionContext = fixture.CreateContext())
        {
            var actor = new BoundaryAuditActorContext("manager-1");
            var stateService = new ArtifactPhotographyStateService(actionContext);
            var useCase = new SetPrimaryArtifactImageUseCase(
                actionContext,
                actor,
                new StaticPermissionChecker([PermissionNames.PhotographyManage]),
                new RecordingAuditWriter(),
                stateService,
                new FixedTimeProvider(UploadedAt));

            var result = await useCase.SetPrimaryArtifactImage(new SetPrimaryArtifactImageCommand(
                seed.ArtifactId,
                imageId,
                ExpectedConcurrencyToken: 0));

            Assert.True(result.Succeeded);
        }

        DocumentationBoundarySnapshot after;
        await using (var afterContext = fixture.CreateContext())
        {
            after = await LoadDocumentationSnapshotAsync(afterContext, seed.DocumentationRecordId);
            var state = await afterContext.ArtifactPhotographyStates.AsNoTracking().SingleAsync(candidate => candidate.ArtifactId == seed.ArtifactId);

            Assert.Equal(imageId, state.PrimaryImageId);
        }

        AssertDocumentationSnapshotUnchanged(before, after);
    }

    [Fact]
    public async Task Documentation_schema_has_no_photography_ownership_or_storage_reference()
    {
        await using var context = fixture.CreateContext();
        var documentationTypes = new[]
        {
            typeof(DocumentationRecord),
            typeof(DocumentationRevision),
            typeof(DocumentationTemplate),
            typeof(DocumentationTemplateVersion),
            typeof(DocumentationTemplateField),
            typeof(DocumentationTemplateFieldOption)
        };
        var photographyTypes = new[]
        {
            typeof(PhotographySet),
            typeof(ArtifactImage),
            typeof(PhotographyRequest),
            typeof(ArtifactPhotographyState),
            typeof(StorageOperationRecovery)
        };
        var forbiddenNames = new[]
        {
            "ArtifactImageId",
            "PhotographySetId",
            "PhotographyRequestId",
            "ArtifactPhotographyStateId",
            "StorageOperationRecoveryId",
            "ImageStorageObjectKey",
            "BucketName",
            "ObjectKey",
            "ProviderUrl",
            "PresignedUrl",
            "MinioEndpoint"
        };

        foreach (var documentationType in documentationTypes)
        {
            var entityType = context.Model.FindEntityType(documentationType);
            Assert.NotNull(entityType);

            Assert.DoesNotContain(entityType!.GetForeignKeys(), foreignKey => photographyTypes.Contains(foreignKey.PrincipalEntityType.ClrType));
            Assert.DoesNotContain(entityType.GetNavigations(), navigation => photographyTypes.Contains(navigation.TargetEntityType.ClrType));
            foreach (var forbiddenName in forbiddenNames)
            {
                Assert.DoesNotContain(entityType.GetProperties(), property => string.Equals(property.Name, forbiddenName, StringComparison.OrdinalIgnoreCase));
            }
        }

        var forbiddenColumnCount = await context.Database.SqlQueryRaw<int>("""
            select count(*)::int as "Value"
            from information_schema.columns
            where table_schema = 'museum'
              and table_name in (
                  'DocumentationRecords',
                  'DocumentationRevisions',
                  'DocumentationTemplates',
                  'DocumentationTemplateVersions',
                  'DocumentationTemplateFields',
                  'DocumentationTemplateFieldOptions')
              and lower(column_name) in (
                  'artifactimageid',
                  'photographysetid',
                  'photographyrequestid',
                  'artifactphotographystateid',
                  'storageoperationrecoveryid',
                  'imagestorageobjectkey',
                  'bucketname',
                  'objectkey',
                  'providerurl',
                  'presignedurl',
                  'minioendpoint')
            """).SingleAsync();

        Assert.Equal(0, forbiddenColumnCount);
        Assert.NotNull(context.Model.FindEntityType(typeof(DocumentationRecord))!.FindProperty(nameof(DocumentationRecord.ArtifactId)));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (createdArtifactIds.Count == 0 && createdTemplateIds.Count == 0)
        {
            return;
        }

        await using var cleanup = fixture.CreateContext();
        var images = await cleanup.ArtifactImages
            .Include(image => image.Derivatives)
            .Where(image => createdArtifactIds.Contains(image.ArtifactId))
            .ToListAsync();
        var versions = await cleanup.DocumentationTemplateVersions
            .Where(version => createdTemplateIds.Contains(version.DocumentationTemplateId))
            .ToListAsync();
        var versionIds = versions.Select(version => version.DocumentationTemplateVersionId).ToArray();
        var fields = await cleanup.DocumentationTemplateFields
            .Where(field => versionIds.Contains(field.DocumentationTemplateVersionId))
            .ToListAsync();
        var fieldIds = fields.Select(field => field.DocumentationTemplateFieldId).ToArray();

        cleanup.PhotographyRequests.RemoveRange(await cleanup.PhotographyRequests
            .Where(request => createdArtifactIds.Contains(request.ArtifactId))
            .ToListAsync());
        cleanup.ArtifactPhotographyStates.RemoveRange(await cleanup.ArtifactPhotographyStates
            .Where(state => createdArtifactIds.Contains(state.ArtifactId))
            .ToListAsync());
        cleanup.ArtifactImageDerivatives.RemoveRange(images.SelectMany(image => image.Derivatives));
        cleanup.ArtifactImages.RemoveRange(images);
        cleanup.PhotographySets.RemoveRange(await cleanup.PhotographySets
            .Where(set => createdArtifactIds.Contains(set.ArtifactId))
            .ToListAsync());
        cleanup.DocumentationRevisions.RemoveRange(await cleanup.DocumentationRevisions
            .Where(revision => versionIds.Contains(revision.TemplateVersionId))
            .ToListAsync());
        cleanup.DocumentationRecords.RemoveRange(await cleanup.DocumentationRecords
            .Where(record => createdArtifactIds.Contains(record.ArtifactId))
            .ToListAsync());
        cleanup.DocumentationTemplateFieldOptions.RemoveRange(await cleanup.DocumentationTemplateFieldOptions
            .Where(option => fieldIds.Contains(option.DocumentationTemplateFieldId))
            .ToListAsync());
        cleanup.DocumentationTemplateFields.RemoveRange(fields);
        cleanup.DocumentationTemplateVersions.RemoveRange(versions);
        cleanup.DocumentationTemplates.RemoveRange(await cleanup.DocumentationTemplates
            .Where(template => createdTemplateIds.Contains(template.DocumentationTemplateId))
            .ToListAsync());
        cleanup.Artifacts.RemoveRange(await cleanup.Artifacts
            .Where(artifact => createdArtifactIds.Contains(artifact.ArtifactId))
            .ToListAsync());
        cleanup.ArtifactCategories.RemoveRange(await cleanup.ArtifactCategories
            .Where(category => createdCategoryIds.Contains(category.CategoryId))
            .ToListAsync());
        cleanup.Locations.RemoveRange(await cleanup.Locations
            .Where(location => createdLocationIds.Contains(location.LocationId))
            .ToListAsync());

        await cleanup.SaveChangesAsync();
    }

    private async Task<DocumentationSeed> SeedDocumentationWithCompletedCorrectionAsync(string prefix)
    {
        await using var context = fixture.CreateContext();
        var category = ArtifactCategory.Create($"{prefix}{Guid.NewGuid():N}"[..8], $"{prefix} documentation category");
        var storage = Location.Create($"{prefix} documentation storage {Guid.NewGuid():N}", LocationType.Storage);
        var artifact = Artifact.Create(category, 1, $"{prefix} documented artifact", storage);
        var template = DocumentationTemplate.Create(category.CategoryId, $"{prefix} condition template", "Feature 002 boundary template", "template-admin");
        var version = template.CreateDraftVersion([
            DocumentationTemplateField.Create("title", "Title", DocumentationFieldType.Text, true, 1, "Main"),
            DocumentationTemplateField.Create("condition", "Condition", DocumentationFieldType.SingleSelect, true, 2, "Main", options:
            [
                DocumentationTemplateFieldOption.Create("good", "Good", 1),
                DocumentationTemplateFieldOption.Create("fair", "Fair", 2)
            ]),
            DocumentationTemplateField.Create("inspection_date", "Inspection Date", DocumentationFieldType.Date, true, 3, "Main"),
            DocumentationTemplateField.Create("notes", "Notes", DocumentationFieldType.MultilineText, false, 4, "Details")
        ], "template-admin");
        template.ActivateVersion(version, "template-admin");

        context.ArtifactCategories.Add(category);
        context.Locations.Add(storage);
        context.Artifacts.Add(artifact);
        context.DocumentationTemplates.Add(template);
        await context.SaveChangesAsync();

        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator-1");
        record.Complete(DocumentationValues("Baseline title", "good", "Initial completed notes"), version, "completer-1");
        record.CorrectCompleted(
            DocumentationValues("Corrected title", "fair", "Corrected notes"),
            version,
            """[{"fieldKey":"title","from":"Baseline title","to":"Corrected title"}]""",
            "Curator corrected the title and condition.",
            "editor-1");
        context.DocumentationRecords.Add(record);
        await context.SaveChangesAsync();

        createdArtifactIds.Add(artifact.ArtifactId);
        createdCategoryIds.Add(category.CategoryId);
        createdLocationIds.Add(storage.LocationId);
        createdTemplateIds.Add(template.DocumentationTemplateId);

        return new DocumentationSeed(
            artifact.ArtifactId,
            template.DocumentationTemplateId,
            version.DocumentationTemplateVersionId,
            record.DocumentationRecordId);
    }

    private async Task<(Guid SetId, Guid ImageId)> PersistPhotographySetWithImageAsync(Guid artifactId, string objectKeyPrefix)
    {
        await using var context = fixture.CreateContext();
        var set = PhotographySet.Create(
            artifactId,
            PhotographyPurpose.GeneralDocumentation,
            new DateOnly(2026, 8, 28),
            "photographer-1",
            "photographer-1");
        var image = ArtifactImage.Create(
            artifactId,
            set.PhotographySetId,
            ImageStorageObjectKey.Create($"artifact-images/{objectKeyPrefix}-{Guid.NewGuid():N}/original.jpg"),
            "front.jpg",
            "image/jpeg",
            2048,
            1200,
            900,
            "photographer-1",
            UploadedAt);

        context.PhotographySets.Add(set);
        context.ArtifactImages.Add(image);
        await context.SaveChangesAsync();

        return (set.PhotographySetId, image.ArtifactImageId);
    }

    private static Dictionary<string, DocumentationFieldValue> DocumentationValues(string title, string condition, string notes) => new()
    {
        ["title"] = DocumentationFieldValue.Text(title),
        ["condition"] = DocumentationFieldValue.SingleSelect(condition),
        ["inspection_date"] = DocumentationFieldValue.Date(new DateOnly(2026, 8, 28)),
        ["notes"] = DocumentationFieldValue.MultilineText(notes)
    };

    private static async Task<DocumentationBoundarySnapshot> LoadDocumentationSnapshotAsync(MuseumDbContext context, Guid documentationRecordId)
    {
        var record = await context.DocumentationRecords
            .AsNoTracking()
            .SingleAsync(candidate => candidate.DocumentationRecordId == documentationRecordId);
        var templateVersion = await context.DocumentationTemplateVersions
            .AsNoTracking()
            .SingleAsync(candidate => candidate.DocumentationTemplateVersionId == record.DocumentationTemplateVersionId);
        var template = await context.DocumentationTemplates
            .AsNoTracking()
            .SingleAsync(candidate => candidate.DocumentationTemplateId == templateVersion.DocumentationTemplateId);
        var fields = await context.DocumentationTemplateFields
            .AsNoTracking()
            .Where(field => field.DocumentationTemplateVersionId == templateVersion.DocumentationTemplateVersionId)
            .OrderBy(field => field.DisplayOrder)
            .ThenBy(field => field.FieldKey)
            .Select(field => new FieldSnapshot(
                field.DocumentationTemplateFieldId,
                field.DocumentationTemplateVersionId,
                field.FieldKey,
                field.Label,
                field.FieldType,
                field.IsRequired,
                field.DisplayOrder,
                field.Section,
                field.HelpText,
                context.DocumentationTemplateFieldOptions
                    .AsNoTracking()
                    .Where(option => option.DocumentationTemplateFieldId == field.DocumentationTemplateFieldId)
                    .OrderBy(option => option.DisplayOrder)
                    .ThenBy(option => option.OptionKey)
                    .Select(option => new FieldOptionSnapshot(
                        option.DocumentationTemplateFieldOptionId,
                        option.DocumentationTemplateFieldId,
                        option.OptionKey,
                        option.Label,
                        option.DisplayOrder))
                    .ToList()))
            .ToListAsync();
        var revisions = await context.DocumentationRevisions
            .AsNoTracking()
            .Where(revision => revision.DocumentationRecordId == record.DocumentationRecordId)
            .OrderBy(revision => revision.RevisionNumber)
            .Select(revision => new RevisionSnapshot(
                revision.DocumentationRevisionId,
                revision.DocumentationRecordId,
                revision.TemplateVersionId,
                revision.RevisionNumber,
                revision.PreviousValuesJson,
                revision.NewValuesJson,
                revision.ChangeSummaryJson,
                revision.Reason,
                revision.CreatedAt,
                revision.CreatedBy))
            .ToListAsync();

        return new DocumentationBoundarySnapshot(
            new RecordSnapshot(
                record.DocumentationRecordId,
                record.ArtifactId,
                record.DocumentationTemplateVersionId,
                record.Status,
                record.ValuesJson,
                record.CompletedBaselineValuesJson,
                record.CreatedAt,
                record.CreatedBy,
                record.LastModifiedAt,
                record.LastModifiedBy,
                record.CompletedAt,
                record.CompletedBy,
                record.ConcurrencyToken),
            new TemplateSnapshot(
                template.DocumentationTemplateId,
                template.ArtifactCategoryId,
                template.Name,
                template.Description,
                template.CreatedAt,
                template.CreatedBy,
                template.LastModifiedAt,
                template.LastModifiedBy,
                new VersionSnapshot(
                    templateVersion.DocumentationTemplateVersionId,
                    templateVersion.DocumentationTemplateId,
                    templateVersion.VersionNumber,
                    templateVersion.Status,
                    templateVersion.ActivatedAt,
                    templateVersion.ActivatedBy,
                    templateVersion.RetiredAt,
                    templateVersion.RetiredBy,
                    templateVersion.CreatedAt,
                    templateVersion.CreatedBy,
                    templateVersion.LastModifiedAt,
                    templateVersion.LastModifiedBy,
                    templateVersion.IsUsed,
                    templateVersion.ConcurrencyToken),
                fields),
            revisions);
    }

    private static void AssertDocumentationSnapshotUnchanged(DocumentationBoundarySnapshot before, DocumentationBoundarySnapshot after)
    {
        AssertRecordSnapshotUnchanged(before.Record, after.Record);
        AssertTemplateSnapshotUnchanged(before.Template, after.Template);
        Assert.Equal(before.Revisions, after.Revisions);
    }

    private static void AssertRecordSnapshotUnchanged(RecordSnapshot before, RecordSnapshot after)
    {
        Assert.Equal(before.DocumentationRecordId, after.DocumentationRecordId);
        Assert.Equal(before.ArtifactId, after.ArtifactId);
        Assert.Equal(before.DocumentationTemplateVersionId, after.DocumentationTemplateVersionId);
        Assert.Equal(before.Status, after.Status);
        AssertJsonEqual(before.ValuesJson, after.ValuesJson);
        AssertJsonEqual(before.CompletedBaselineValuesJson, after.CompletedBaselineValuesJson);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.CreatedBy, after.CreatedBy);
        Assert.Equal(before.LastModifiedAt, after.LastModifiedAt);
        Assert.Equal(before.LastModifiedBy, after.LastModifiedBy);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
        Assert.Equal(before.CompletedBy, after.CompletedBy);
        Assert.Equal(before.ConcurrencyToken, after.ConcurrencyToken);
    }

    private static void AssertTemplateSnapshotUnchanged(TemplateSnapshot before, TemplateSnapshot after)
    {
        Assert.Equal(before.TemplateId, after.TemplateId);
        Assert.Equal(before.ArtifactCategoryId, after.ArtifactCategoryId);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Description, after.Description);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(before.CreatedBy, after.CreatedBy);
        Assert.Equal(before.LastModifiedAt, after.LastModifiedAt);
        Assert.Equal(before.LastModifiedBy, after.LastModifiedBy);
        Assert.Equal(before.Version, after.Version);
        Assert.Equal(before.Fields.Count, after.Fields.Count);
        for (var index = 0; index < before.Fields.Count; index++)
        {
            AssertFieldSnapshotUnchanged(before.Fields[index], after.Fields[index]);
        }
    }

    private static void AssertFieldSnapshotUnchanged(FieldSnapshot before, FieldSnapshot after)
    {
        Assert.Equal(before.DocumentationTemplateFieldId, after.DocumentationTemplateFieldId);
        Assert.Equal(before.DocumentationTemplateVersionId, after.DocumentationTemplateVersionId);
        Assert.Equal(before.FieldKey, after.FieldKey);
        Assert.Equal(before.Label, after.Label);
        Assert.Equal(before.FieldType, after.FieldType);
        Assert.Equal(before.IsRequired, after.IsRequired);
        Assert.Equal(before.DisplayOrder, after.DisplayOrder);
        Assert.Equal(before.Section, after.Section);
        Assert.Equal(before.HelpText, after.HelpText);
        Assert.Equal(before.Options, after.Options);
    }

    private static void AssertJsonEqual(string? expected, string? actual)
    {
        if (expected is null || actual is null)
        {
            Assert.Equal(expected, actual);
            return;
        }

        using var expectedDocument = JsonDocument.Parse(expected);
        using var actualDocument = JsonDocument.Parse(actual);
        Assert.True(JsonElement.DeepEquals(expectedDocument.RootElement, actualDocument.RootElement));
    }

    private sealed class StaticPermissionChecker(IReadOnlyCollection<string> permissions) : ICurrentActorPermissionChecker
    {
        private readonly HashSet<string> permissions = new(permissions, StringComparer.Ordinal);

        public bool HasPermission(string permissionName) => permissions.Contains(permissionName);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Guid.NewGuid().ToString("N"));
    }

    private sealed class BoundaryAuditActorContext(string userId) : IAuditActorContext
    {
        public AuditActor CurrentActor => new(userId, "Documentation boundary user", true);
    }

    private sealed record DocumentationSeed(
        Guid ArtifactId,
        Guid TemplateId,
        Guid TemplateVersionId,
        Guid DocumentationRecordId);

    private sealed record DocumentationBoundarySnapshot(
        RecordSnapshot Record,
        TemplateSnapshot Template,
        IReadOnlyList<RevisionSnapshot> Revisions);

    private sealed record RecordSnapshot(
        Guid DocumentationRecordId,
        Guid ArtifactId,
        Guid DocumentationTemplateVersionId,
        DocumentationRecordStatus Status,
        string ValuesJson,
        string? CompletedBaselineValuesJson,
        DateTimeOffset CreatedAt,
        string? CreatedBy,
        DateTimeOffset? LastModifiedAt,
        string? LastModifiedBy,
        DateTimeOffset? CompletedAt,
        string? CompletedBy,
        int ConcurrencyToken);

    private sealed record TemplateSnapshot(
        Guid TemplateId,
        Guid ArtifactCategoryId,
        string Name,
        string? Description,
        DateTimeOffset CreatedAt,
        string? CreatedBy,
        DateTimeOffset? LastModifiedAt,
        string? LastModifiedBy,
        VersionSnapshot Version,
        IReadOnlyList<FieldSnapshot> Fields);

    private sealed record VersionSnapshot(
        Guid DocumentationTemplateVersionId,
        Guid DocumentationTemplateId,
        int VersionNumber,
        DocumentationTemplateVersionStatus Status,
        DateTimeOffset? ActivatedAt,
        string? ActivatedBy,
        DateTimeOffset? RetiredAt,
        string? RetiredBy,
        DateTimeOffset CreatedAt,
        string? CreatedBy,
        DateTimeOffset? LastModifiedAt,
        string? LastModifiedBy,
        bool IsUsed,
        int ConcurrencyToken);

    private sealed record FieldSnapshot(
        Guid DocumentationTemplateFieldId,
        Guid DocumentationTemplateVersionId,
        string FieldKey,
        string Label,
        DocumentationFieldType FieldType,
        bool IsRequired,
        int DisplayOrder,
        string Section,
        string? HelpText,
        IReadOnlyList<FieldOptionSnapshot> Options);

    private sealed record FieldOptionSnapshot(
        Guid DocumentationTemplateFieldOptionId,
        Guid DocumentationTemplateFieldId,
        string OptionKey,
        string Label,
        int DisplayOrder);

    private sealed record RevisionSnapshot(
        Guid DocumentationRevisionId,
        Guid DocumentationRecordId,
        Guid TemplateVersionId,
        int RevisionNumber,
        string PreviousValuesJson,
        string NewValuesJson,
        string ChangeSummaryJson,
        string Reason,
        DateTimeOffset CreatedAt,
        string? CreatedBy);
}

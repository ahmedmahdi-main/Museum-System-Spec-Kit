using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Audit;

namespace MuseumSystem.Integration.Tests.Documentation;

[Collection(PostgresDocumentationCollection.Name)]
public sealed class DocumentationConcurrencyTests(PostgresDocumentationTestFixture fixture)
{
    [Fact]
    public async Task Create_documentation_record_competing_insert_clears_losing_context_and_reloads_winner()
    {
        Guid artifactId;
        string? originalHolder;
        Guid? originalLocation;
        int originalMovementCount;
        await using (var seed = fixture.CreateContext())
        {
            var (artifact, _) = await DocumentationTestData.SeedReadyGraphAsync(seed, $"CR{Guid.NewGuid():N}"[..8]);
            artifact.DeliverToInternalHolder(MovementRecipientType.DocumentationDivision, "Documentation");
            await seed.SaveChangesAsync();
            artifactId = artifact.ArtifactId;
            originalHolder = artifact.CurrentHolderType;
            originalLocation = artifact.CurrentLocationId;
            originalMovementCount = await seed.MovementRecords.CountAsync();
        }

        var pauseBeforeLosingSave = new PauseBeforeSaveInterceptor();
        var losingOptions = new DbContextOptionsBuilder<Infrastructure.Persistence.MuseumDbContext>(fixture.Options)
            .AddInterceptors(pauseBeforeLosingSave)
            .Options;
        await using var losingContext = new Infrastructure.Persistence.MuseumDbContext(losingOptions);
        await using var winningContext = fixture.CreateContext();

        var losingTask = CreateUseCase(losingContext).CreateDocumentationRecord(new(artifactId));
        await pauseBeforeLosingSave.WaitUntilSavingAsync();

        var winningResult = await CreateUseCase(winningContext).CreateDocumentationRecord(new(artifactId));
        pauseBeforeLosingSave.Release();
        var losingResult = await losingTask;

        Assert.True(winningResult.Succeeded);
        Assert.NotNull(winningResult.AuditReference);
        Assert.False(losingResult.Succeeded);
        Assert.True(losingResult.ConcurrencyConflict);
        Assert.Null(losingResult.AuditReference);
        Assert.Contains("Documentation Record was created", Assert.Single(losingResult.Messages));
        Assert.False(losingContext.ChangeTracker.HasChanges());
        Assert.Empty(losingContext.ChangeTracker.Entries());

        var records = await losingContext.DocumentationRecords
            .Where(record => record.ArtifactId == artifactId)
            .ToListAsync();
        var persisted = Assert.Single(records);
        Assert.Equal(winningResult.Value!.Record.DocumentationRecordId, persisted.DocumentationRecordId);
        Assert.DoesNotContain(losingContext.ChangeTracker.Entries<DocumentationRecord>(), entry => entry.State == EntityState.Added);

        var auditEntries = await losingContext.AuditEntries.AsNoTracking()
            .Where(item => item.ActionName == DocumentationAuditActions.RecordCreate &&
                item.EntityId == persisted.DocumentationRecordId.ToString())
            .ToListAsync();
        var artifactAfter = await losingContext.Artifacts.AsNoTracking().SingleAsync(item => item.ArtifactId == artifactId);
        Assert.Single(auditEntries);
        Assert.Equal(originalHolder, artifactAfter.CurrentHolderType);
        Assert.Equal(originalLocation, artifactAfter.CurrentLocationId);
        Assert.Equal(originalMovementCount, await losingContext.MovementRecords.CountAsync());
        Assert.False(losingContext.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Completed_documentation_correction_reports_database_race_without_failed_side_effects()
    {
        Guid recordId;
        Guid artifactId;
        int expectedToken;
        string? originalHolder;
        Guid? originalLocation;
        int originalMovementCount;
        await using (var seed = fixture.CreateContext())
        {
            var (artifact, version) = await DocumentationTestData.SeedReadyGraphAsync(seed, $"RC{Guid.NewGuid():N}"[..8]);
            var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
            record.Complete(DocumentationTestData.CompletedValues("Baseline"), version, "completer");
            seed.DocumentationRecords.Add(record);
            await seed.SaveChangesAsync();
            recordId = record.DocumentationRecordId;
            artifactId = artifact.ArtifactId;
            expectedToken = record.ConcurrencyToken;
            originalHolder = artifact.CurrentHolderType;
            originalLocation = artifact.CurrentLocationId;
            originalMovementCount = await seed.MovementRecords.CountAsync();
        }

        var pauseBeforeSecondSave = new PauseBeforeSaveInterceptor();
        var secondOptions = new DbContextOptionsBuilder<Infrastructure.Persistence.MuseumDbContext>(fixture.Options)
            .AddInterceptors(pauseBeforeSecondSave)
            .Options;
        await using var firstContext = fixture.CreateContext();
        await using var secondContext = new Infrastructure.Persistence.MuseumDbContext(secondOptions);
        var secondTask = UseCase(secondContext).CorrectCompletedDocumentation(
            new(recordId, expectedToken, Values("Second correction"), "Second reason"));

        await pauseBeforeSecondSave.WaitUntilSavingAsync();
        var firstResult = await UseCase(firstContext).CorrectCompletedDocumentation(
            new(recordId, expectedToken, Values("First correction"), "First reason"));
        pauseBeforeSecondSave.Release();
        var secondResult = await secondTask;

        Assert.True(firstResult.Succeeded);
        Assert.NotNull(firstResult.AuditReference);
        Assert.False(secondResult.Succeeded);
        Assert.True(secondResult.ConcurrencyConflict);
        Assert.Null(secondResult.AuditReference);
        Assert.False(secondContext.ChangeTracker.HasChanges());
        Assert.Empty(secondContext.ChangeTracker.Entries());

        var persisted = await secondContext.DocumentationRecords
            .Include(item => item.Revisions)
            .SingleAsync(item => item.DocumentationRecordId == recordId);
        var artifactAfter = await secondContext.Artifacts.AsNoTracking().SingleAsync(item => item.ArtifactId == artifactId);
        var auditEntries = await secondContext.AuditEntries.AsNoTracking()
            .Where(item => item.ActionName == DocumentationAuditActions.RecordCorrectCompleted &&
                item.EntityId == recordId.ToString())
            .ToListAsync();
        Assert.Contains("First correction", persisted.ValuesJson);
        Assert.DoesNotContain("Second correction", persisted.ValuesJson);
        Assert.Equal(firstResult.Value!.Record.ConcurrencyToken, persisted.ConcurrencyToken);
        var revision = Assert.Single(persisted.Revisions);
        Assert.Equal(2, revision.RevisionNumber);
        Assert.Equal("First reason", revision.Reason);
        Assert.DoesNotContain(persisted.Revisions, item => item.Reason == "Second reason");
        Assert.Equal(DocumentationRecordStatus.Completed, persisted.Status);
        Assert.Equal(originalHolder, artifactAfter.CurrentHolderType);
        Assert.Equal(originalLocation, artifactAfter.CurrentLocationId);
        Assert.Equal(originalMovementCount, await secondContext.MovementRecords.CountAsync());
        var auditEntry = Assert.Single(auditEntries);
        Assert.Contains("Revision 2", auditEntry.Summary);
        Assert.DoesNotContain("Second reason", auditEntry.ChangeSummary ?? string.Empty);
        Assert.False(secondContext.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Documentation_record_uses_optimistic_concurrency_token()
    {
        Guid recordId;
        await using (var seed = fixture.CreateContext())
        {
            var (artifact, version) = await DocumentationTestData.SeedReadyGraphAsync(seed, $"C{Guid.NewGuid():N}"[..8]);
            var record = DocumentationRecord.Create(artifact.ArtifactId, version);
            recordId = record.DocumentationRecordId;
            seed.DocumentationRecords.Add(record);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await firstContext.DocumentationRecords.Include(r => r.DocumentationTemplateVersion).ThenInclude(v => v!.Fields).SingleAsync(r => r.DocumentationRecordId == recordId);
        var second = await secondContext.DocumentationRecords.Include(r => r.DocumentationTemplateVersion).ThenInclude(v => v!.Fields).SingleAsync(r => r.DocumentationRecordId == recordId);

        first.SaveDraft(new Dictionary<string, DocumentationFieldValue> { ["title"] = DocumentationFieldValue.Text("First") }, first.DocumentationTemplateVersion!);
        await firstContext.SaveChangesAsync();

        second.SaveDraft(new Dictionary<string, DocumentationFieldValue> { ["title"] = DocumentationFieldValue.Text("Second") }, second.DocumentationTemplateVersion!);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Documentation_template_version_uses_optimistic_concurrency_token_for_stale_draft_changes()
    {
        Guid templateId;
        await using (var seed = fixture.CreateContext())
        {
            var category = ArtifactCategory.Create($"TC{Guid.NewGuid():N}"[..8], "Template concurrency category");
            var template = DocumentationTemplate.Create(category.CategoryId, "Template concurrency");
            template.CreateDraftVersion([DocumentationTemplateField.Create("initial", "Initial", DocumentationFieldType.Text, true, 1, "Main")]);
            templateId = template.DocumentationTemplateId;
            seed.ArtifactCategories.Add(category);
            seed.DocumentationTemplates.Add(template);
            await seed.SaveChangesAsync();
        }

        await using var firstContext = fixture.CreateContext();
        await using var secondContext = fixture.CreateContext();
        var first = await LoadTemplateAsync(firstContext, templateId);
        var second = await LoadTemplateAsync(secondContext, templateId);
        var firstVersion = first.Versions.Single();
        var secondVersion = second.Versions.Single();

        first.ActivateVersion(firstVersion, "first-manager");
        await firstContext.SaveChangesAsync();

        second.ActivateVersion(secondVersion, "second-manager");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    private static Task<DocumentationTemplate> LoadTemplateAsync(DbContext context, Guid templateId) =>
        context.Set<DocumentationTemplate>()
            .Include(template => template.Versions)
            .ThenInclude(version => version.Fields)
            .SingleAsync(template => template.DocumentationTemplateId == templateId);

    private static CorrectCompletedDocumentationUseCase UseCase(Infrastructure.Persistence.MuseumDbContext context)
    {
        var actorContext = new TestAuditActorContext();
        return new(context, new DocumentationChangeSummaryService(), new AuditWriter(context, actorContext), actorContext);
    }

    private static CreateDocumentationRecordUseCase CreateUseCase(Infrastructure.Persistence.MuseumDbContext context)
    {
        var actorContext = new TestAuditActorContext();
        return new(
            context,
            new DocumentationTemplateResolver(context),
            new DocumentationAvailabilityService(),
            new AuditWriter(context, actorContext),
            actorContext);
    }

    private static IReadOnlyList<DocumentationFieldValueInputDto> Values(string title) =>
    [
        new DocumentationFieldValueInputDto { FieldKey = "title", TextValue = title },
        new DocumentationFieldValueInputDto { FieldKey = "condition", OptionKey = "good" }
    ];

    private sealed class PauseBeforeSaveInterceptor : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _saving = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitUntilSavingAsync() => _saving.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void Release() => _release.TrySetResult();

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _saving.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class TestAuditActorContext : IAuditActorContext
    {
        public AuditActor CurrentActor => new("race-editor", "Race Editor", true);
    }
}

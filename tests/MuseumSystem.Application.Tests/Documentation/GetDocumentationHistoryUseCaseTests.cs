using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class GetDocumentationHistoryUseCaseTests
{
    [Fact]
    public async Task Rejects_record_not_found()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();

        var result = await UseCase(db).GetDocumentationHistory(new(Guid.NewGuid()));

        AssertIssue(result, "DocumentationRecord.NotFound");
    }

    [Fact]
    public async Task Rejects_record_that_is_not_completed()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        var record = DocumentationRecord.Create(artifact.ArtifactId, version, "creator");
        db.DocumentationRecords.Add(record);
        await db.SaveChangesAsync();

        var result = await UseCase(db).GetDocumentationHistory(new(record.DocumentationRecordId));

        AssertIssue(result, "DocumentationRecord.NotCompleted");
    }

    [Fact]
    public async Task Rejects_missing_completion_baseline()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, _) = await PhaseDTestData.CompletedRecordAsync(db);
        db.Entry(record).Property(item => item.CompletedBaselineValuesJson).CurrentValue = null;
        await db.SaveChangesAsync();

        var result = await UseCase(db).GetDocumentationHistory(new(record.DocumentationRecordId));

        AssertIssue(result, "DocumentationRecord.BaselineMissing");
    }

    [Fact]
    public async Task Rejects_persisted_correction_with_invalid_revision_number()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Reason"));
        var revision = Assert.Single(record.Revisions);
        db.Entry(revision).Property(item => item.RevisionNumber).CurrentValue = 1;
        await db.SaveChangesAsync();

        var result = await UseCase(db).GetDocumentationHistory(new(record.DocumentationRecordId));

        AssertIssue(result, "DocumentationRecord.HistoryInvalid");
    }

    [Fact]
    public async Task Rejects_record_whose_bound_template_version_is_missing()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, version, _) = await PhaseDTestData.CompletedRecordAsync(db);
        var recordId = record.DocumentationRecordId;
        var versionId = version.DocumentationTemplateVersionId;
        db.ChangeTracker.Clear();
        var persistedVersion = await db.DocumentationTemplateVersions.FindAsync(versionId);
        Assert.NotNull(persistedVersion);
        db.DocumentationTemplateVersions.Remove(persistedVersion);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await UseCase(db).GetDocumentationHistory(new(recordId));

        AssertIssue(result, "DocumentationRecord.NotFound");
    }

    [Fact]
    public async Task Returns_completion_baseline_then_ordered_corrections()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var correction = PhaseDTestData.CorrectUseCase(db, audit);
        await correction.CorrectCompletedDocumentation(new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "First reason"));
        await correction.CorrectCompletedDocumentation(new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("good"), "Second reason"));

        var result = await UseCase(db)
            .GetDocumentationHistory(new(record.DocumentationRecordId));

        Assert.True(result.Succeeded);
        var items = Assert.IsAssignableFrom<IReadOnlyList<MuseumSystem.Application.Modules.Documentation.Contracts.DocumentationHistoryItemDto>>(result.Value);
        Assert.Equal([1, 2, 3], items.Select(item => item.RevisionNumber));
        var baseline = items[0];
        Assert.True(baseline.IsCompletionBaseline);
        Assert.Null(baseline.Reason);
        Assert.Equal(record.CompletedBy, baseline.Author);
        Assert.Equal(record.CompletedAt, baseline.Timestamp);
        Assert.All(items.Skip(1), item => { Assert.False(item.IsCompletionBaseline); Assert.False(string.IsNullOrWhiteSpace(item.Reason)); Assert.NotEmpty(item.ChangedFields); });
    }

    private static GetDocumentationHistoryUseCase UseCase(Infrastructure.Persistence.MuseumDbContext db) =>
        new(db, new DocumentationChangeSummaryService());

    private static void AssertIssue<T>(Common.UseCaseResult<T> result, string code)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(code, Assert.Single(result.ValidationIssues).Code);
    }
}

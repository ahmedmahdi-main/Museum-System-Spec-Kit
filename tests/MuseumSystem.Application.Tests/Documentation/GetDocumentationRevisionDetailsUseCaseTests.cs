using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class GetDocumentationRevisionDetailsUseCaseTests
{
    [Fact]
    public async Task Rejects_invalid_revision_number()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(Guid.NewGuid(), 0));

        AssertIssue(result, "DocumentationRevision.InvalidNumber");
    }

    [Fact]
    public async Task Rejects_record_not_found()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(Guid.NewGuid(), 1));

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

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 1));

        AssertIssue(result, "DocumentationRecord.NotCompleted");
    }

    [Fact]
    public async Task Rejects_missing_completion_baseline_for_revision_one()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, _) = await PhaseDTestData.CompletedRecordAsync(db);
        db.Entry(record).Property(item => item.CompletedBaselineValuesJson).CurrentValue = null;
        await db.SaveChangesAsync();

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 1));

        AssertIssue(result, "DocumentationRecord.BaselineMissing");
    }

    [Fact]
    public async Task Rejects_persisted_correction_with_missing_reason()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Reason"));
        var revision = Assert.Single(record.Revisions);
        db.Entry(revision).Property(item => item.Reason).CurrentValue = "";
        await db.SaveChangesAsync();

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 2));

        AssertIssue(result, "DocumentationRevision.ReasonMissing");
    }

    [Fact]
    public async Task Rejects_missing_referenced_template_source_data()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, version, _) = await PhaseDTestData.CompletedRecordAsync(db);
        var recordId = record.DocumentationRecordId;
        var templateId = version.DocumentationTemplateId;
        db.ChangeTracker.Clear();
        var template = await db.DocumentationTemplates.FindAsync(templateId);
        Assert.NotNull(template);
        db.DocumentationTemplates.Remove(template);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(recordId, 1));

        AssertIssue(result, "DocumentationRecord.ReferenceMissing");
    }

    [Fact]
    public async Task Rejects_missing_referenced_template_category_source_data()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, version, _) = await PhaseDTestData.CompletedRecordAsync(db);
        var recordId = record.DocumentationRecordId;
        var template = await db.DocumentationTemplates.FindAsync(version.DocumentationTemplateId);
        Assert.NotNull(template);
        var categoryId = template.ArtifactCategoryId;
        db.ChangeTracker.Clear();
        var category = await db.ArtifactCategories.FindAsync(categoryId);
        Assert.NotNull(category);
        db.ArtifactCategories.Remove(category);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(recordId, 1));

        AssertIssue(result, "DocumentationRecord.ReferenceMissing");
    }

    [Fact]
    public async Task Rejects_correction_revision_not_found()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, _) = await PhaseDTestData.CompletedRecordAsync(db);

        var result = await UseCase(db).GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 99));

        AssertIssue(result, "DocumentationRevision.NotFound");
    }

    [Fact]
    public async Task Returns_bound_baseline_and_correction_details_without_fabricated_metadata()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, version, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Correction reason"));
        var useCase = UseCase(db);

        var baseline = await useCase.GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 1));
        var correction = await useCase.GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 2));
        var missing = await useCase.GetDocumentationRevisionDetails(new(record.DocumentationRecordId, 99));

        Assert.True(baseline.Succeeded);
        Assert.True(baseline.Value!.IsCompletionBaseline);
        Assert.Null(baseline.Value.Reason);
        Assert.NotEmpty(baseline.Value.BaselineValues);
        Assert.Equal(version.DocumentationTemplateVersionId, baseline.Value.TemplateVersion.DocumentationTemplateVersionId);
        Assert.True(correction.Succeeded);
        Assert.Equal("Correction reason", correction.Value!.Reason);
        var change = Assert.Single(correction.Value.ChangedFields);
        Assert.Equal("Condition label", change.FieldLabel);
        Assert.Contains("Good label", change.Summary);
        Assert.Contains("Fair label", change.Summary);
        Assert.False(missing.Succeeded);
    }

    private static GetDocumentationRevisionDetailsUseCase UseCase(Infrastructure.Persistence.MuseumDbContext db) =>
        new(db, new DocumentationChangeSummaryService());

    private static void AssertIssue<T>(Common.UseCaseResult<T> result, string code)
    {
        Assert.False(result.Succeeded);
        Assert.Equal(code, Assert.Single(result.ValidationIssues).Code);
    }
}

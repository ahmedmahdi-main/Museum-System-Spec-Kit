using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class CorrectCompletedDocumentationUseCaseTests
{
    public static TheoryData<IReadOnlyList<DocumentationFieldValueInputDto>> InvalidValues
    {
        get
        {
            var data = new TheoryData<IReadOnlyList<DocumentationFieldValueInputDto>>();
            data.Add([new DocumentationFieldValueInputDto { FieldKey = "condition", TextValue = "fair" }]);
            data.Add([new DocumentationFieldValueInputDto { FieldKey = "unknown", OptionKey = "fair" }]);
            data.Add([new DocumentationFieldValueInputDto { FieldKey = "condition", OptionKey = "unknown-option" }]);
            return data;
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_missing_reason_without_side_effects(string? reason)
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var before = record.ValuesJson;

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), reason!));

        Assert.False(result.Succeeded);
        Assert.Contains(result.ValidationIssues, issue => issue.Field == nameof(CorrectCompletedDocumentationRequest.Reason));
        Assert.Equal(before, record.ValuesJson);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.Empty(record.Revisions);
        Assert.Empty(audit.Requests);
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Fact]
    public async Task Corrects_completed_record_in_storehouse_without_movement_or_custody_changes()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var artifact = await db.Artifacts.FindAsync(record.ArtifactId);
        var holder = artifact!.CurrentHolderType;
        var location = artifact.CurrentLocationId;
        var movements = db.MovementRecords.Count();

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "  Updated assessment  "));

        Assert.True(result.Succeeded, string.Join(" | ", result.ValidationIssues.Select(issue => issue.Message).Concat(result.Messages)));
        Assert.Equal(2, result.Value!.RevisionNumber);
        Assert.Equal("Updated assessment", Assert.Single(record.Revisions).Reason);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.Equal(ArtifactCurrentStatus.InStorage, artifact.CurrentStatus);
        Assert.Equal(holder, artifact.CurrentHolderType);
        Assert.Equal(location, artifact.CurrentLocationId);
        Assert.Equal(movements, db.MovementRecords.Count());
        Assert.Contains(audit.Requests, entry => entry.ActionName == DocumentationAuditActions.RecordCorrectCompleted);
    }

    [Fact]
    public async Task Rejects_stale_token_without_overwriting_values_or_creating_revision()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var before = record.ValuesJson;

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken - 1, PhaseDTestData.Value("fair"), "Stale reason"));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Equal(before, record.ValuesJson);
        Assert.Empty(record.Revisions);
        Assert.Empty(audit.Requests);
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public async Task Rejects_invalid_correction_values_without_side_effects(IReadOnlyList<DocumentationFieldValueInputDto> invalidValues)
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var recordId = record.DocumentationRecordId;
        var valuesBefore = record.ValuesJson;
        var tokenBefore = record.ConcurrencyToken;

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, invalidValues, "Invalid correction"));

        Assert.False(result.Succeeded);
        Assert.Equal("DocumentationRecord.ValuesInvalid", Assert.Single(result.ValidationIssues).Code);
        Assert.Empty(record.Revisions);
        Assert.Equal(valuesBefore, record.ValuesJson);
        Assert.Equal(tokenBefore, record.ConcurrencyToken);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.Empty(audit.Requests);
        Assert.False(db.ChangeTracker.HasChanges());

        db.ChangeTracker.Clear();
        var persisted = await db.DocumentationRecords.FindAsync(recordId);
        Assert.NotNull(persisted);
        Assert.Equal(valuesBefore, persisted.ValuesJson);
        Assert.Equal(tokenBefore, persisted.ConcurrencyToken);
        Assert.Equal(DocumentationRecordStatus.Completed, persisted.Status);
        Assert.Empty(db.DocumentationRevisions.Where(revision => revision.DocumentationRecordId == recordId));
    }

    [Fact]
    public async Task Corrects_completed_record_held_by_another_division_without_changing_holder()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var artifact = Assert.IsType<Artifact>(await db.Artifacts.FindAsync(record.ArtifactId));
        artifact.DeliverToInternalHolder(MovementRecipientType.LaboratoryDivision, "Laboratory");
        await db.SaveChangesAsync();
        var token = artifact.ConcurrencyToken;

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Laboratory finding"));

        Assert.True(result.Succeeded);
        Assert.Equal(MovementRecipientType.LaboratoryDivision.ToString(), artifact.CurrentHolderType);
        Assert.Equal("Laboratory", artifact.CurrentHolderName);
        Assert.Null(artifact.CurrentLocationId);
        Assert.Equal(token, artifact.ConcurrencyToken);
        Assert.Empty(db.MovementRecords);
    }

    [Fact]
    public async Task Accepts_correction_reason_at_exactly_1000_characters()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var reason = new string('ر', 1000);

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), reason));

        Assert.True(result.Succeeded);
        Assert.Equal(reason, Assert.Single(record.Revisions).Reason);
        Assert.Single(audit.Requests);
    }

    [Fact]
    public async Task Rejects_correction_reason_over_1000_characters_without_side_effects()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var valuesBefore = record.ValuesJson;
        var tokenBefore = record.ConcurrencyToken;

        var result = await PhaseDTestData.CorrectUseCase(db, audit).CorrectCompletedDocumentation(
            new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), new string('ر', 1001)));

        Assert.False(result.Succeeded);
        var issue = Assert.Single(result.ValidationIssues);
        Assert.Equal(nameof(CorrectCompletedDocumentationRequest.Reason), issue.Field);
        Assert.Equal("DocumentationCorrection.ReasonTooLong", issue.Code);
        Assert.Empty(record.Revisions);
        Assert.Equal(valuesBefore, record.ValuesJson);
        Assert.Equal(tokenBefore, record.ConcurrencyToken);
        Assert.Equal(DocumentationRecordStatus.Completed, record.Status);
        Assert.Empty(audit.Requests);
        Assert.False(db.ChangeTracker.HasChanges());
    }
}

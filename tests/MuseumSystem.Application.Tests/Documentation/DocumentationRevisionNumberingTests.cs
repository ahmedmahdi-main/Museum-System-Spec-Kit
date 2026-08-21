namespace MuseumSystem.Application.Tests.Documentation;

public sealed class DocumentationRevisionNumberingTests
{
    [Fact]
    public async Task Completion_is_revision_one_and_three_corrections_create_two_three_four()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var (record, _, audit) = await PhaseDTestData.CompletedRecordAsync(db);
        var useCase = PhaseDTestData.CorrectUseCase(db, audit);

        var two = await useCase.CorrectCompletedDocumentation(new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Second"));
        var three = await useCase.CorrectCompletedDocumentation(new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("good"), "Third"));
        var four = await useCase.CorrectCompletedDocumentation(new(record.DocumentationRecordId, record.ConcurrencyToken, PhaseDTestData.Value("fair"), "Fourth"));

        Assert.Equal([2, 3, 4], new[] { two.Value!.RevisionNumber, three.Value!.RevisionNumber, four.Value!.RevisionNumber });
        Assert.Equal([2, 3, 4], record.Revisions.Select(item => item.RevisionNumber));
        Assert.DoesNotContain(record.Revisions, item => item.RevisionNumber == 1);
    }
}

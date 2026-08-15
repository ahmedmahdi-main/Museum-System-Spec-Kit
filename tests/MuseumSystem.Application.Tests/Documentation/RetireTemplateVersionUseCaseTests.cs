using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class RetireTemplateVersionUseCaseTests
{
    [Fact]
    public async Task Retires_active_version_without_replacement_and_preserves_zero_active_state()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var active = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        template.ActivateVersion(active, "tester");
        await db.SaveChangesAsync();

        var result = await new RetireTemplateVersionUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).RetireTemplateVersion(
            new RetireTemplateVersionRequest(active.DocumentationTemplateVersionId, active.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal(DocumentationTemplateVersionStatus.Retired, active.Status);
        Assert.DoesNotContain(template.Versions, version => version.Status == DocumentationTemplateVersionStatus.Active);
        Assert.Contains("no Active", result.Messages[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_retiring_non_active_version()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        await db.SaveChangesAsync();

        var result = await new RetireTemplateVersionUseCase(db, new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext()).RetireTemplateVersion(
            new RetireTemplateVersionRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken));

        Assert.False(result.Succeeded);
        Assert.Equal("DocumentationTemplateVersion.RetirementInvalid", result.ValidationIssues[0].Code);
    }
}

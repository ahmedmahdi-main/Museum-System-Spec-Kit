using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class ActivateTemplateVersionUseCaseTests
{
    [Fact]
    public async Task First_ever_activation_reports_no_previous_active_and_uses_actor_metadata()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var draft = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField()]);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditWriter();

        var result = await new ActivateTemplateVersionUseCase(db, audit, DocumentationApplicationTestHost.ActorContext("activator-1", "Activator")).ActivateTemplateVersion(
            new ActivateTemplateVersionRequest(draft.DocumentationTemplateVersionId, draft.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal(DocumentationTemplateVersionStatus.Active, result.Value!.Status);
        Assert.Equal("activator-1", draft.ActivatedBy);
        Assert.Contains("version 1 activated", result.Messages[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("previous Active", result.Messages[0], StringComparison.OrdinalIgnoreCase);
        var auditRequest = Assert.Single(audit.Requests, request => request.ActionName == DocumentationAuditActions.TemplateVersionActivate);
        Assert.Equal("No previous Active version existed.", auditRequest.ChangeSummary);
    }

    [Fact]
    public async Task Replacement_activation_reports_exact_previous_active_version_retired_atomically()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var template = DocumentationApplicationTestHost.AddTemplate(db, category);
        var first = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField("first")]);
        var second = DocumentationApplicationTestHost.AddDraft(db, template, [DocumentationApplicationTestHost.BasicField("second")]);
        template.ActivateVersion(first, "tester");
        await db.SaveChangesAsync();
        var audit = new RecordingAuditWriter();

        var result = await new ActivateTemplateVersionUseCase(db, audit, DocumentationApplicationTestHost.ActorContext("activator-2", "Activator")).ActivateTemplateVersion(
            new ActivateTemplateVersionRequest(second.DocumentationTemplateVersionId, second.ConcurrencyToken));

        Assert.True(result.Succeeded);
        Assert.Equal(DocumentationTemplateVersionStatus.Retired, first.Status);
        Assert.Equal(DocumentationTemplateVersionStatus.Active, second.Status);
        Assert.Equal("activator-2", first.RetiredBy);
        Assert.Equal("activator-2", second.ActivatedBy);
        Assert.Single(template.Versions, version => version.Status == DocumentationTemplateVersionStatus.Active);
        Assert.Contains("version 1 retired atomically", result.Messages[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version 2 activated", result.Messages[0], StringComparison.OrdinalIgnoreCase);
        var auditRequest = Assert.Single(audit.Requests, request => request.ActionName == DocumentationAuditActions.TemplateVersionActivate);
        Assert.Contains("previous Active version 1", auditRequest.Summary);
        Assert.Contains("activated version 2", auditRequest.ChangeSummary);
    }
}
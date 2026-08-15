using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class TemplateAuditUseCaseTests
{
    [Fact]
    public async Task Template_write_use_cases_create_audit_records_and_context_actor_metadata()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditWriter();
        var actorContext = DocumentationApplicationTestHost.ActorContext("template-manager-1", "Template Manager");

        var createTemplate = await new CreateDocumentationTemplateUseCase(db, audit, actorContext).CreateDocumentationTemplate(
            new CreateDocumentationTemplateRequest(category.CategoryId, "Template", null));
        var template = db.DocumentationTemplates.Single();
        var createDraft = await new CreateTemplateVersionDraftUseCase(db, audit, actorContext).CreateTemplateVersionDraft(
            new CreateTemplateVersionDraftRequest(createTemplate.Value!.DocumentationTemplateId));
        var draft = db.DocumentationTemplateVersions.Single();
        var saveDraft = await new SaveTemplateVersionDraftUseCase(db, audit, actorContext).SaveTemplateVersionDraft(
            new SaveTemplateVersionDraftRequest(createDraft.Value!.DocumentationTemplateVersionId, createDraft.Value.ConcurrencyToken, DocumentationApplicationTestHost.SevenFieldInputs()));
        var activate = await new ActivateTemplateVersionUseCase(db, audit, actorContext).ActivateTemplateVersion(
            new ActivateTemplateVersionRequest(saveDraft.Value!.DocumentationTemplateVersionId, saveDraft.Value.ConcurrencyToken));
        var retire = await new RetireTemplateVersionUseCase(db, audit, actorContext).RetireTemplateVersion(
            new RetireTemplateVersionRequest(activate.Value!.DocumentationTemplateVersionId, activate.Value.ConcurrencyToken));

        Assert.True(retire.Succeeded);
        Assert.Equal("template-manager-1", template.CreatedBy);
        Assert.Equal("template-manager-1", draft.CreatedBy);
        Assert.Equal("template-manager-1", draft.LastModifiedBy);
        Assert.Equal("template-manager-1", draft.ActivatedBy);
        Assert.Equal("template-manager-1", draft.RetiredBy);
        Assert.Contains(audit.Requests, request => request.ActionName == DocumentationAuditActions.TemplateCreate);
        Assert.Contains(audit.Requests, request => request.ActionName == DocumentationAuditActions.TemplateVersionSaveDraft);
        Assert.Contains(audit.Requests, request => request.ActionName == DocumentationAuditActions.TemplateVersionActivate);
        Assert.Contains(audit.Requests, request => request.ActionName == DocumentationAuditActions.TemplateVersionRetire);
        Assert.All(audit.Requests, request => Assert.Equal("Documentation", request.ModuleName));
    }
}
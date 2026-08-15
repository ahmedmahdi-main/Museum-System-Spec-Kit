using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Documentation;

public sealed class CreateDocumentationRecordUseCaseTests
{
    [Fact]
    public async Task Creates_one_draft_record_from_active_category_template_with_authenticated_actor_metadata_and_audit()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        var version = DocumentationApplicationTestHost.AddActiveTemplateVersion(db, category);
        await db.SaveChangesAsync();
        var audit = new RecordingAuditWriter();
        var useCase = NewUseCase(db, audit);

        var result = await useCase.CreateDocumentationRecord(new CreateDocumentationRecordRequest(artifact.ArtifactId));
        var duplicate = await useCase.CreateDocumentationRecord(new CreateDocumentationRecordRequest(artifact.ArtifactId));

        Assert.True(result.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.Equal(version.DocumentationTemplateVersionId, result.Value!.Record.DocumentationTemplateVersionId);
        Assert.Equal("user-1", db.DocumentationRecords.Single().CreatedBy);
        Assert.Contains(audit.Requests, request => request.ActionName == DocumentationAuditActions.RecordCreate);
        Assert.Equal("DocumentationRecord.AlreadyExists", duplicate.ValidationIssues[0].Code);
    }

    [Fact]
    public async Task Fails_when_category_has_no_active_template()
    {
        await using var db = DocumentationApplicationTestHost.CreateDbContext();
        var category = DocumentationApplicationTestHost.AddCategory(db);
        var storage = DocumentationApplicationTestHost.AddStorageLocation(db);
        var artifact = DocumentationApplicationTestHost.AddArtifact(db, category, storage);
        DocumentationApplicationTestHost.HoldByDocumentation(artifact);
        await db.SaveChangesAsync();

        var result = await NewUseCase(db).CreateDocumentationRecord(new CreateDocumentationRecordRequest(artifact.ArtifactId));

        Assert.False(result.Succeeded);
        Assert.Equal("DocumentationTemplate.ActiveMissing", result.ValidationIssues[0].Code);
    }

    [Fact]
    public async Task Classifies_template_version_concurrency_as_reload_conflict_not_duplicate_record()
    {
        var databaseName = Guid.NewGuid().ToString();
        var root = new InMemoryDatabaseRoot();
        Guid artifactId;
        await using (var seed = CreateContext(databaseName, root))
        {
            var category = DocumentationApplicationTestHost.AddCategory(seed);
            var storage = DocumentationApplicationTestHost.AddStorageLocation(seed);
            var artifact = DocumentationApplicationTestHost.AddArtifact(seed, category, storage);
            DocumentationApplicationTestHost.HoldByDocumentation(artifact);
            DocumentationApplicationTestHost.AddActiveTemplateVersion(seed, category);
            artifactId = artifact.ArtifactId;
            await seed.SaveChangesAsync();
        }

        await using var db = CreateContext(databaseName, root, throwConcurrencyOnSave: true);
        var result = await NewUseCase(db).CreateDocumentationRecord(new CreateDocumentationRecordRequest(artifactId));

        Assert.False(result.Succeeded);
        Assert.True(result.ConcurrencyConflict);
        Assert.Contains("Active template version changed", result.Messages[0]);
        Assert.DoesNotContain("Documentation Record was created", result.Messages[0]);
    }

    private static CreateDocumentationRecordUseCase NewUseCase(MuseumDbContext db, RecordingAuditWriter? audit = null) =>
        new(db, new DocumentationTemplateResolver(db), new DocumentationAvailabilityService(), audit ?? new RecordingAuditWriter(), DocumentationApplicationTestHost.ActorContext());

    private static MuseumDbContext CreateContext(string databaseName, InMemoryDatabaseRoot root, bool throwConcurrencyOnSave = false)
    {
        var builder = new DbContextOptionsBuilder<MuseumDbContext>().UseInMemoryDatabase(databaseName, root);
        if (throwConcurrencyOnSave)
        {
            builder.AddInterceptors(new ConcurrencyThrowingSaveChangesInterceptor());
        }

        return new MuseumDbContext(builder.Options);
    }

    private sealed class ConcurrencyThrowingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateConcurrencyException("Template version changed.");
    }
}

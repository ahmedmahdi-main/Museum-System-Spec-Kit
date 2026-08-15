using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Documentation.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Application.Tests.Documentation;

internal static class DocumentationApplicationTestHost
{
    public static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MuseumDbContext(options);
    }

    public static ArtifactCategory AddCategory(MuseumDbContext db, string code = "CER")
    {
        var category = ArtifactCategory.Create(code, $"{code} category");
        db.ArtifactCategories.Add(category);
        return category;
    }

    public static DocumentationTemplate AddTemplate(MuseumDbContext db, ArtifactCategory category)
    {
        var template = DocumentationTemplate.Create(category.CategoryId, $"{category.CategoryCode} documentation", "Template description", "tester");
        db.DocumentationTemplates.Add(template);
        return template;
    }

    public static DocumentationTemplateVersion AddDraft(MuseumDbContext db, DocumentationTemplate template, IReadOnlyList<DocumentationTemplateField>? fields = null)
    {
        var draft = template.CreateDraftVersion(fields, "tester");
        db.DocumentationTemplateVersions.Add(draft);
        return draft;
    }

    public static IAuditActorContext ActorContext(string? userId = "user-1", string displayName = "Test Manager") =>
        new TestAuditActorContext(userId, displayName);

    public static DocumentationTemplateField BasicField(string key = "title") =>
        DocumentationTemplateField.Create(key, "Title", DocumentationFieldType.Text, true, 1, "Main", "Short title");

    public static IReadOnlyList<DocumentationTemplateFieldInputDto> SevenFieldInputs() =>
    [
        new("text", "Text", DocumentationFieldType.Text, true, 1, "Identity", "Short text", []),
        new("multiline", "Multiline", DocumentationFieldType.MultilineText, false, 2, "Identity", "Long text", []),
        new("number", "Number", DocumentationFieldType.Number, false, 3, "Measurements", null, []),
        new("date", "Date", DocumentationFieldType.Date, false, 4, "Measurements", null, []),
        new("boolean", "Boolean", DocumentationFieldType.Boolean, false, 5, "Flags", null, []),
        new("single", "Single", DocumentationFieldType.SingleSelect, true, 6, "Options", null,
        [
            new DocumentationTemplateFieldOptionInputDto("a", "A", 1),
            new DocumentationTemplateFieldOptionInputDto("b", "B", 2)
        ]),
        new("many", "Many", DocumentationFieldType.MultiSelect, false, 7, "Options", null,
        [
            new DocumentationTemplateFieldOptionInputDto("x", "X", 1),
            new DocumentationTemplateFieldOptionInputDto("y", "Y", 2)
        ])
    ];
}

internal sealed class TestAuditActorContext(string? userId, string displayName) : IAuditActorContext
{
    public AuditActor CurrentActor => new(userId, displayName, true);
}

internal sealed class RecordingAuditWriter : IAuditWriter
{
    private int sequence;

    public List<AuditWriteRequest> Requests { get; } = [];

    public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        sequence++;
        return Task.FromResult($"audit-{sequence}");
    }
}

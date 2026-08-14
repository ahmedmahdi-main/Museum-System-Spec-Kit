using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.ArtifactRegistry;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Application.Modules.StorehouseOperations;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MuseumSystem.Application.Tests.Audit;

public sealed class SensitiveWriteAuditTests
{
    [Fact]
    public async Task Failed_sensitive_writes_do_not_emit_success_audit_entries()
    {
        await using var db = CreateDbContext();
        var auditWriter = new RecordingAuditWriter();
        db.ArtifactCategories.Add(ArtifactCategory.Create("DUP", "Duplicate"));
        var unvalidatedBatch = ImportBatch.Create("import.xlsx");
        unvalidatedBatch.AddRow(ImportRow.Create(2, "DUP", "1", "Shelf A", "Artifact"));
        db.ImportBatches.Add(unvalidatedBatch);
        await db.SaveChangesAsync();

        var duplicateCategory = await new CategoryUseCases(db, auditWriter).CreateCategory(new CreateCategoryRequest("DUP", "Duplicate again", null));
        var emptyDelivery = await new DeliverArtifactsUseCase(db, auditWriter).DeliverArtifacts(new DeliverArtifactsRequest(
            [],
            MovementRecipientType.DocumentationDivision,
            "Documentation",
            null,
            "Study"));
        var notReadyCommit = await new CommitImportBatchUseCase(db, auditWriter).CommitImportBatch(unvalidatedBatch.ImportBatchId);

        Assert.False(duplicateCategory.Succeeded);
        Assert.False(emptyDelivery.Succeeded);
        Assert.False(notReadyCommit.Succeeded);
        Assert.Empty(auditWriter.Requests);
    }

    private static MuseumDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MuseumDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MuseumDbContext(options);
    }

    private sealed class RecordingAuditWriter : IAuditWriter
    {
        public List<AuditWriteRequest> Requests { get; } = [];

        public Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult($"audit-{Requests.Count}");
        }
    }
}
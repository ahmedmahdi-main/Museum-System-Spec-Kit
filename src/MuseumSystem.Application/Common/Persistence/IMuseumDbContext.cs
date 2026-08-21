using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Application.Common.Persistence;

public interface IMuseumDbContext
{
    DbSet<ArtifactCategory> ArtifactCategories { get; }
    DbSet<Artifact> Artifacts { get; }
    DbSet<Location> Locations { get; }
    DbSet<MovementRecord> MovementRecords { get; }
    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<ImportRow> ImportRows { get; }
    DbSet<ReconciliationSession> ReconciliationSessions { get; }
    DbSet<ReconciliationResult> ReconciliationResults { get; }
    DbSet<DocumentedCorrection> DocumentedCorrections { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<DocumentationTemplate> DocumentationTemplates { get; }
    DbSet<DocumentationTemplateVersion> DocumentationTemplateVersions { get; }
    DbSet<DocumentationTemplateField> DocumentationTemplateFields { get; }
    DbSet<DocumentationTemplateFieldOption> DocumentationTemplateFieldOptions { get; }
    DbSet<DocumentationRecord> DocumentationRecords { get; }
    DbSet<DocumentationRevision> DocumentationRevisions { get; }

    Task<IMuseumDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IMuseumDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

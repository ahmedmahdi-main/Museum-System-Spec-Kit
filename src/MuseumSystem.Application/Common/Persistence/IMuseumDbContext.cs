using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.Photography;

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
    DbSet<PhotographySet> PhotographySets { get; }
    DbSet<ArtifactImage> ArtifactImages { get; }
    DbSet<ArtifactImageDerivative> ArtifactImageDerivatives { get; }
    DbSet<ArtifactPhotographyState> ArtifactPhotographyStates { get; }
    DbSet<PhotographyUploadOperation> PhotographyUploadOperations { get; }
    DbSet<PhotographyUploadFileOutcome> PhotographyUploadFileOutcomes { get; }
    DbSet<StorageOperationRecovery> StorageOperationRecoveries { get; }

    Task<IMuseumDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    void ClearTrackedChanges();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IMuseumDbTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}

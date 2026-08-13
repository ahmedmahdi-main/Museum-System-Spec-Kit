using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.IdentityAccess;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

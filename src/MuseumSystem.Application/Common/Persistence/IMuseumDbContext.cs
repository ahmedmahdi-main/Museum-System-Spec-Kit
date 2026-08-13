using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Domain.Modules.Import;

namespace MuseumSystem.Application.Common.Persistence;

public interface IMuseumDbContext
{
    DbSet<ArtifactCategory> ArtifactCategories { get; }
    DbSet<Artifact> Artifacts { get; }
    DbSet<Location> Locations { get; }
    DbSet<MovementRecord> MovementRecords { get; }
    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<ImportRow> ImportRows { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

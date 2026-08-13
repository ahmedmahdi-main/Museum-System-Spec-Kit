using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Common.Persistence;

public interface IMuseumDbContext
{
    DbSet<ArtifactCategory> ArtifactCategories { get; }
    DbSet<Artifact> Artifacts { get; }
    DbSet<Location> Locations { get; }
    DbSet<MovementRecord> MovementRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}


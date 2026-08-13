using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Infrastructure.Identity;

namespace MuseumSystem.Infrastructure.Persistence;

public sealed class MuseumDbContext(DbContextOptions<MuseumDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IMuseumDbContext
{
    public DbSet<ArtifactCategory> ArtifactCategories => Set<ArtifactCategory>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<MovementRecord> MovementRecords => Set<MovementRecord>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportRow> ImportRows => Set<ImportRow>();
    public DbSet<ReconciliationSession> ReconciliationSessions => Set<ReconciliationSession>();
    public DbSet<ReconciliationResult> ReconciliationResults => Set<ReconciliationResult>();
    public DbSet<DocumentedCorrection> DocumentedCorrections => Set<DocumentedCorrection>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("museum");
        builder.ApplyConfigurationsFromAssembly(typeof(MuseumDbContext).Assembly);
    }
}

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Domain.Modules.Import;
using MuseumSystem.Domain.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Documentation;
using MuseumSystem.Domain.Modules.Photography;
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
    public DbSet<DocumentationTemplate> DocumentationTemplates => Set<DocumentationTemplate>();
    public DbSet<DocumentationTemplateVersion> DocumentationTemplateVersions => Set<DocumentationTemplateVersion>();
    public DbSet<DocumentationTemplateField> DocumentationTemplateFields => Set<DocumentationTemplateField>();
    public DbSet<DocumentationTemplateFieldOption> DocumentationTemplateFieldOptions => Set<DocumentationTemplateFieldOption>();
    public DbSet<DocumentationRecord> DocumentationRecords => Set<DocumentationRecord>();
    public DbSet<DocumentationRevision> DocumentationRevisions => Set<DocumentationRevision>();
    public DbSet<PhotographyRequest> PhotographyRequests => Set<PhotographyRequest>();
    public DbSet<PhotographySet> PhotographySets => Set<PhotographySet>();
    public DbSet<ArtifactImage> ArtifactImages => Set<ArtifactImage>();
    public DbSet<ArtifactImageDerivative> ArtifactImageDerivatives => Set<ArtifactImageDerivative>();
    public DbSet<ArtifactPhotographyState> ArtifactPhotographyStates => Set<ArtifactPhotographyState>();
    public DbSet<PhotographyUploadOperation> PhotographyUploadOperations => Set<PhotographyUploadOperation>();
    public DbSet<PhotographyUploadFileOutcome> PhotographyUploadFileOutcomes => Set<PhotographyUploadFileOutcome>();
    public DbSet<StorageOperationRecovery> StorageOperationRecoveries => Set<StorageOperationRecovery>();

    public void ClearTrackedChanges() => ChangeTracker.Clear();

    public async Task<IMuseumDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return new NoOpMuseumDbTransaction(this);
        }

        return new MuseumDbTransaction(this, await Database.BeginTransactionAsync(cancellationToken));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("museum");
        builder.ApplyConfigurationsFromAssembly(typeof(MuseumDbContext).Assembly);
    }

    private sealed class MuseumDbTransaction(MuseumDbContext context, IDbContextTransaction transaction) : IMuseumDbTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private sealed class NoOpMuseumDbTransaction(MuseumDbContext context) : IMuseumDbTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            context.ChangeTracker.Clear();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

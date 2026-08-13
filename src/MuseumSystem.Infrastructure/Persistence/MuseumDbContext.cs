using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;
using MuseumSystem.Infrastructure.Identity;

namespace MuseumSystem.Infrastructure.Persistence;

public sealed class MuseumDbContext(DbContextOptions<MuseumDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IMuseumDbContext
{
    public DbSet<ArtifactCategory> ArtifactCategories => Set<ArtifactCategory>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<Location> Locations => Set<Location>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("museum");
        builder.ApplyConfigurationsFromAssembly(typeof(MuseumDbContext).Assembly);
    }
}

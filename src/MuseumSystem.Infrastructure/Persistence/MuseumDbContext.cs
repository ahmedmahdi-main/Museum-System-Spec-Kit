using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MuseumSystem.Infrastructure.Identity;

namespace MuseumSystem.Infrastructure.Persistence;

public sealed class MuseumDbContext(DbContextOptions<MuseumDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("museum");
        builder.ApplyConfigurationsFromAssembly(typeof(MuseumDbContext).Assembly);
    }
}

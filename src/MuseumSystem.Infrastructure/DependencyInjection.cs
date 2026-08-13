using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MuseumSystem.Infrastructure.Identity;
using MuseumSystem.Infrastructure.Persistence;

namespace MuseumSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMuseumInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MuseumDatabase")
            ?? throw new InvalidOperationException("Connection string 'MuseumDatabase' is not configured.");

        services.AddDbContext<MuseumDbContext>(options => options.UseNpgsql(connectionString));

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MuseumDbContext>()
            .AddSignInManager();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();

        return services;
    }
}

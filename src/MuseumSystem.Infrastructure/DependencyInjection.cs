using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Infrastructure.Excel;
using MuseumSystem.Infrastructure.Identity;
using MuseumSystem.Infrastructure.Persistence;
using MuseumSystem.Infrastructure.Audit;

namespace MuseumSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMuseumInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MuseumDatabase")
            ?? throw new InvalidOperationException("Connection string 'MuseumDatabase' is not configured.");

        services.AddHttpContextAccessor();
        services.AddDbContext<MuseumDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IMuseumDbContext>(provider => provider.GetRequiredService<MuseumDbContext>());
        services.AddScoped<IExcelImportReader, ClosedXmlImportReader>();
        services.AddScoped<IAuditActorContext, HttpAuditActorContext>();
        services.AddScoped<IAuditWriter, AuditWriter>();

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MuseumDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        return services;
    }
}
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Photography;
using MuseumSystem.Application.Modules.Photography.Imaging;
using MuseumSystem.Application.Modules.Photography.Storage;
using MuseumSystem.Infrastructure.Excel;
using MuseumSystem.Infrastructure.Identity;
using MuseumSystem.Infrastructure.Persistence;
using MuseumSystem.Infrastructure.Audit;
using MuseumSystem.Infrastructure.Photography.Imaging;
using MuseumSystem.Infrastructure.Photography.Storage;

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
        services.AddScoped<ICurrentActorPermissionChecker, HttpCurrentActorPermissionChecker>();
        services.AddScoped<PhotographyUploadFingerprintService>();
        services.AddScoped<IArtifactImageProcessor, ArtifactImageProcessor>();
        services.AddScoped<IArtifactImageStorage, MinioArtifactImageStorage>();
        services.AddSingleton<IValidateOptions<ArtifactImageProcessingOptions>, ArtifactImageProcessingOptionsValidator>();
        services.Configure<ArtifactImageProcessingOptions>(options =>
        {
            options.MaximumOriginalBytes = configuration.GetValue<long?>("Photography:Uploads:MaximumOriginalBytes") ?? options.MaximumOriginalBytes;
            options.Thumbnail = new DerivativeOptions(
                configuration.GetValue<int?>("Photography:Derivatives:Thumbnail:MaxWidth") ?? options.Thumbnail.MaxWidth,
                configuration.GetValue<int?>("Photography:Derivatives:Thumbnail:MaxHeight") ?? options.Thumbnail.MaxHeight,
                configuration.GetValue<int?>("Photography:Derivatives:Thumbnail:JpegQuality") ?? options.Thumbnail.JpegQuality);
            options.Preview = new DerivativeOptions(
                configuration.GetValue<int?>("Photography:Derivatives:Preview:MaxWidth") ?? options.Preview.MaxWidth,
                configuration.GetValue<int?>("Photography:Derivatives:Preview:MaxHeight") ?? options.Preview.MaxHeight,
                configuration.GetValue<int?>("Photography:Derivatives:Preview:JpegQuality") ?? options.Preview.JpegQuality);
        });
        services.AddSingleton<IValidateOptions<MinioArtifactImageStorageOptions>, MinioArtifactImageStorageOptionsValidator>();
        services.AddOptions<MinioArtifactImageStorageOptions>()
            .Bind(configuration.GetSection(MinioArtifactImageStorageOptions.SectionName));
        services.AddSingleton<IValidateOptions<PhotographyIdempotencyOptions>, PhotographyIdempotencyOptionsValidator>();
        services.AddOptions<PhotographyIdempotencyOptions>()
            .Bind(configuration.GetSection(PhotographyIdempotencyOptions.SectionName));

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

using Microsoft.Extensions.DependencyInjection;
using MuseumSystem.Application.Modules.ArtifactRegistry;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Application.Modules.StorehouseOperations;

namespace MuseumSystem.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMuseumApplication(this IServiceCollection services)
    {
        services.AddIdentityAccessApplication();
        services.AddScoped<CategoryUseCases>();
        services.AddScoped<ArtifactWriteUseCases>();
        services.AddScoped<ArtifactReadUseCases>();
        services.AddScoped<LocationUseCases>();
        services.AddScoped<MovementHistoryUseCase>();
        services.AddScoped<ReturnArtifactsUseCase>();
        services.AddScoped<ReturnEligibilityUseCase>();
        services.AddScoped<DeliverArtifactsUseCase>();
        services.AddScoped<DeliveryEligibilityUseCase>();
        services.AddScoped<CancelImportBatchUseCase>();
        services.AddScoped<CommitImportBatchUseCase>();
        services.AddScoped<ValidateImportBatchUseCase>();
        services.AddScoped<UploadImportFileForPreviewUseCase>();
        return services;
    }
}

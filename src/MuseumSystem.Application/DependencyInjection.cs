using Microsoft.Extensions.DependencyInjection;
using MuseumSystem.Application.Modules.Audit;
using MuseumSystem.Application.Modules.ArtifactRegistry;
using MuseumSystem.Application.Modules.Documentation;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Application.Modules.Import;
using MuseumSystem.Application.Modules.Photography;
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
        services.AddScoped<AuditTrailUseCase>();
        services.AddScoped<TemplateQueryUseCases>();
        services.AddScoped<DocumentationAvailabilityService>();
        services.AddScoped<DocumentationTemplateResolver>();
        services.AddScoped<SearchDocumentationArtifactUseCase>();
        services.AddScoped<GetDocumentationWorkspaceUseCase>();
        services.AddScoped<CreateDocumentationRecordUseCase>();
        services.AddScoped<GetDocumentationRecordForEditUseCase>();
        services.AddScoped<SaveDocumentationDraftUseCase>();
        services.AddScoped<CompleteDocumentationRecordUseCase>();
        services.AddScoped<DocumentationChangeSummaryService>();
        services.AddScoped<CorrectCompletedDocumentationUseCase>();
        services.AddScoped<GetDocumentationHistoryUseCase>();
        services.AddScoped<GetDocumentationRevisionDetailsUseCase>();
        services.AddScoped<CreateDocumentationTemplateUseCase>();
        services.AddScoped<CreateTemplateVersionDraftUseCase>();
        services.AddScoped<SaveTemplateVersionDraftUseCase>();
        services.AddScoped<ActivateTemplateVersionUseCase>();
        services.AddScoped<RetireTemplateVersionUseCase>();
        services.AddScoped<CreateDocumentedCorrectionUseCase>();
        services.AddScoped<ReviewReconciliationResultsUseCase>();
        services.AddScoped<RecordReconciliationItemsUseCase>();
        services.AddScoped<StartReconciliationSessionUseCase>();
        services.AddScoped<PhotographyObjectKeyFactory>();
        services.AddScoped<PhotographyUploadPersistenceService>();
        services.AddScoped<PhotographyUploadAuditService>();
        services.AddScoped<PhotographyResponseMapper>();
        return services;
    }
}

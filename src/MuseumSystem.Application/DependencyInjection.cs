using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.AddScoped<PhotographyUploadConsistencyService>();
        services.AddScoped<PhotographyResponseMapper>();
        services.AddScoped<CreatePhotographySetWithImagesUseCase>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CreatePhotographyRequestUseCase>();
        services.AddScoped<CancelPhotographyRequestUseCase>();
        services.AddScoped<CompletePhotographyRequestUseCase>();
        services.AddScoped<PhotographyRequestQueries>();
        services.AddScoped<PhotographyGalleryMapper>();
        services.AddScoped<ViewArtifactImagesUseCase>();
        services.AddScoped<UpdateArtifactImageMetadataUseCase>();
        services.AddScoped<SetPrimaryArtifactImageUseCase>();
        services.AddScoped<ArtifactPhotographyStateService>();
        services.AddScoped<PrimaryImageProjectionQueries>();
        services.AddScoped<ArtifactImageDeletionFinalizationService>();
        services.AddScoped<ArtifactImageDeletionService>();
        services.AddScoped<DeleteArtifactImageByUploaderGraceUseCase>();
        services.AddScoped<DeleteArtifactImagePrivilegedUseCase>();
        services.AddScoped<StorageOperationRecoveryUseCase>();
        return services;
    }
}

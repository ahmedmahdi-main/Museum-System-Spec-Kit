using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.ArtifactRegistry.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.ArtifactRegistry;

public sealed class ArtifactWriteUseCases(IMuseumDbContext dbContext, IAuditWriter? auditWriter = null)
{
    public async Task<UseCaseResult<ArtifactDetailsDto>> CreateArtifact(CreateArtifactRequest request, CancellationToken cancellationToken = default)
    {
        var requestValidation = ValidateCreateRequest(request);
        if (requestValidation.Count > 0)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(requestValidation.ToArray());
        }

        var category = await dbContext.ArtifactCategories.FindAsync([request.CategoryId], cancellationToken);
        if (category is null || !category.IsActive)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("Category.NotSelectable", "اختر فئة فعالة للقطعة.", nameof(request.CategoryId)));
        }

        var location = await dbContext.Locations.FindAsync([request.InitialLocationId], cancellationToken);
        if (location is null || !location.IsActive || location.LocationType != LocationType.Storage)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("Location.NotSelectable", "اختر موقع خزن فعال.", nameof(request.InitialLocationId)));
        }

        var duplicate = await dbContext.Artifacts.AnyAsync(a => a.CategoryId == request.CategoryId && a.ItemNumber == request.ItemNumber, cancellationToken);
        if (duplicate)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("MuseumNumber.Duplicate", "رقم القطعة مستخدم ضمن هذه الفئة.", nameof(request.ItemNumber)));
        }

        var artifact = ArtifactFactory.Create(category, request.ItemNumber, request.BasicDescription, location);
        dbContext.Artifacts.Add(artifact);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Artifact.Create", artifact.ArtifactId, $"Created artifact {artifact.MuseumNumberDisplay}.", $"CategoryId={artifact.CategoryId}; ItemNumber={artifact.ItemNumber}; InitialLocationId={artifact.CurrentLocationId}", cancellationToken);

        return UseCaseResult<ArtifactDetailsDto>.Success(ToDetailsDto(artifact, category, location));
    }

    public async Task<UseCaseResult<ArtifactDetailsDto>> UpdateArtifactBasicInfo(UpdateArtifactBasicInfoRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.BasicDescription))
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("Artifact.BasicDescriptionRequired", "الوصف الأساسي مطلوب.", nameof(request.BasicDescription)));
        }

        var artifact = await dbContext.Artifacts
            .Include(a => a.Category)
            .Include(a => a.CurrentLocation)
            .FirstOrDefaultAsync(a => a.ArtifactId == request.ArtifactId, cancellationToken);

        if (artifact is null || artifact.Category is null)
        {
            return UseCaseResult<ArtifactDetailsDto>.Failure(new ValidationIssue("Artifact.NotFound", "القطعة غير موجودة.", nameof(request.ArtifactId)));
        }

        artifact.UpdateBasicDescription(request.BasicDescription);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync("Artifact.UpdateBasicInfo", artifact.ArtifactId, $"Updated artifact {artifact.MuseumNumberDisplay} basic information.", "BasicDescription updated.", cancellationToken);
        return UseCaseResult<ArtifactDetailsDto>.Success(ToDetailsDto(artifact, artifact.Category, artifact.CurrentLocation));
    }

    private static List<ValidationIssue> ValidateCreateRequest(CreateArtifactRequest request)
    {
        List<ValidationIssue> issues = [];
        if (request.CategoryId == Guid.Empty)
        {
            issues.Add(new ValidationIssue("Category.Required", "اختر فئة القطعة.", nameof(request.CategoryId)));
        }

        if (request.ItemNumber <= 0)
        {
            issues.Add(new ValidationIssue("ItemNumber.Positive", "رقم التسلسل داخل الفئة يجب أن يكون أكبر من صفر.", nameof(request.ItemNumber)));
        }

        if (request.InitialLocationId == Guid.Empty)
        {
            issues.Add(new ValidationIssue("Location.Required", "اختر موقع الخزن الأولي.", nameof(request.InitialLocationId)));
        }

        if (string.IsNullOrWhiteSpace(request.BasicDescription))
        {
            issues.Add(new ValidationIssue("Artifact.BasicDescriptionRequired", "الوصف الأساسي مطلوب.", nameof(request.BasicDescription)));
        }

        return issues;
    }

    private static ArtifactDetailsDto ToDetailsDto(Artifact artifact, ArtifactCategory category, Location? location) =>
        new(
            artifact.ArtifactId,
            artifact.CategoryId,
            category.CategoryCode,
            artifact.ItemNumber,
            artifact.MuseumNumberDisplay,
            artifact.BasicDescription,
            artifact.CurrentStatus,
            artifact.CurrentLocationId,
            location?.NameArabic,
            artifact.CurrentHolderType,
            artifact.CurrentHolderName,
            artifact.LastKnownStorageLocationId);

    private Task WriteAuditAsync(string actionName, Guid artifactId, string summary, string? changeSummary, CancellationToken cancellationToken) =>
        auditWriter?.WriteAsync(new AuditWriteRequest(
            actionName,
            "ArtifactRegistry",
            nameof(Artifact),
            artifactId.ToString(),
            summary,
            changeSummary), cancellationToken) ?? Task.CompletedTask;
}

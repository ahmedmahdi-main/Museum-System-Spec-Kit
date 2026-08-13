using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class CreateDocumentedCorrectionUseCase(IMuseumDbContext dbContext, IAuditWriter auditWriter)
{
    public async Task<UseCaseResult<DocumentedCorrectionDto>> CreateDocumentedCorrection(CreateDocumentedCorrectionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await dbContext.ReconciliationResults.FirstOrDefaultAsync(r => r.ReconciliationResultId == request.ReconciliationResultId, cancellationToken);
        if (result is null || result.ArtifactId is null)
        {
            return UseCaseResult<DocumentedCorrectionDto>.Failure(new ValidationIssue("Correction.ResultInvalid", "اختر تعارضاً مؤكداً."));
        }

        try
        {
            DocumentedCorrectionRules.EnsureCanCreateFromConflict(result, request.Reason);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return UseCaseResult<DocumentedCorrectionDto>.Failure(new ValidationIssue("Correction.NotAllowed", ex.Message));
        }

        var artifact = await dbContext.Artifacts.FirstOrDefaultAsync(a => a.ArtifactId == result.ArtifactId.Value, cancellationToken);
        if (artifact is null)
        {
            return UseCaseResult<DocumentedCorrectionDto>.Failure(new ValidationIssue("Artifact.NotFound", "القطعة غير موجودة."));
        }

        var previousSummary = CurrentStateSummary(artifact);
        Location? location = null;
        if (request.NewLocationId is not null)
        {
            location = await dbContext.Locations.FirstOrDefaultAsync(l => l.LocationId == request.NewLocationId, cancellationToken);
            if (location is null)
            {
                return UseCaseResult<DocumentedCorrectionDto>.Failure(new ValidationIssue("Correction.LocationInvalid", "الموقع غير موجود."));
            }
        }

        try
        {
            ApplyCorrection(artifact, request, location);
        }
        catch (InvalidOperationException ex)
        {
            return UseCaseResult<DocumentedCorrectionDto>.Failure(new ValidationIssue("Correction.NotAllowed", ex.Message));
        }

        var newSummary = CurrentStateSummary(artifact);
        var correction = DocumentedCorrection.Create(
            artifact.ArtifactId,
            DocumentedCorrectionSourceType.Reconciliation,
            result.ReconciliationResultId,
            request.CorrectionType,
            previousSummary,
            newSummary,
            request.Reason);
        dbContext.DocumentedCorrections.Add(correction);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UseCaseResult<DocumentedCorrectionDto>.Conflict("تعذر التصحيح لأن حالة القطعة تغيرت. أعد المحاولة.");
        }

        var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
            "DocumentedCorrection.Create",
            "StorehouseOperations",
            nameof(DocumentedCorrection),
            correction.CorrectionId.ToString(),
            $"تم إنشاء تصحيح موثق للقطعة {artifact.MuseumNumberDisplay}.",
            $"{previousSummary} -> {newSummary}"), cancellationToken);

        return UseCaseResult<DocumentedCorrectionDto>.Success(ToDto(correction), "تم حفظ التصحيح الموثق.", auditReference);
    }

    private static void ApplyCorrection(Artifact artifact, CreateDocumentedCorrectionRequest request, Location? location)
    {
        switch (request.CorrectionType)
        {
            case DocumentedCorrectionType.LocationCorrection:
                if (location is null)
                {
                    throw new InvalidOperationException("اختر الموقع الجديد.");
                }

                if (DocumentedCorrectionRules.WouldSubstituteReturn(artifact, location))
                {
                    throw new InvalidOperationException("استخدم الاستلام عند عودة القطعة فعلياً للمخزن.");
                }

                if (location.LocationType == LocationType.Storage)
                {
                    artifact.CorrectStorageLocation(location);
                }
                else
                {
                    artifact.CorrectDisplayLocation(location);
                }
                break;

            case DocumentedCorrectionType.HolderCorrection:
                if (request.NewHolderType is null || string.IsNullOrWhiteSpace(request.NewHolderName))
                {
                    throw new InvalidOperationException("اكتب جهة العهدة الجديدة.");
                }

                artifact.CorrectInternalHolder(request.NewHolderType.Value, request.NewHolderName);
                break;

            default:
                throw new InvalidOperationException("نوع التصحيح غير مدعوم في هذه المرحلة.");
        }
    }

    private static string CurrentStateSummary(Artifact artifact) =>
        $"Status={artifact.CurrentStatus}; CurrentLocation={artifact.CurrentLocationId}; Holder={artifact.CurrentHolderType}/{artifact.CurrentHolderName}; LastStorage={artifact.LastKnownStorageLocationId}";

    private static DocumentedCorrectionDto ToDto(DocumentedCorrection correction) => new(
        correction.CorrectionId,
        correction.ArtifactId,
        correction.CorrectionType,
        correction.PreviousValueSummary,
        correction.NewValueSummary,
        correction.Reason,
        correction.CorrectedAt);
}

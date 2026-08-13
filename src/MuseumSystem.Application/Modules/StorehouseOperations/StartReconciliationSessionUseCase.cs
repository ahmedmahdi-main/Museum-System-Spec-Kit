using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.StorehouseOperations.Contracts;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

public sealed class StartReconciliationSessionUseCase(IMuseumDbContext dbContext)
{
    public async Task<UseCaseResult<ReconciliationSessionDto>> StartReconciliationSession(StartReconciliationSessionRequest request, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.Locations.FirstOrDefaultAsync(l => l.LocationId == request.LocationId, cancellationToken);
        if (location is null || !CurrentStateRules.IsValidStorageLocation(location))
        {
            return UseCaseResult<ReconciliationSessionDto>.Failure(new ValidationIssue("Reconciliation.LocationInvalid", "اختر موقع خزن صالح."));
        }

        var session = ReconciliationSession.Start(location, request.Note);
        dbContext.ReconciliationSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return UseCaseResult<ReconciliationSessionDto>.Success(ReconciliationDtoMapper.ToDto(session), "تم بدء الجرد.");
    }
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Application.Modules.StorehouseOperations;

internal static class ReconciliationDtoMapper
{
    public static Contracts.ReconciliationSessionDto ToDto(ReconciliationSession session) => new(
        session.ReconciliationSessionId,
        session.LocationId,
        session.Location?.NameArabic,
        session.Status,
        session.Note,
        session.Results.OrderBy(result => result.ResultType).ThenBy(result => result.ObservedMuseumNumber).Select(ToDto).ToList());

    public static Contracts.ReconciliationResultDto ToDto(ReconciliationResult result) => new(
        result.ReconciliationResultId,
        result.ArtifactId,
        result.ObservedMuseumNumber,
        result.ExpectedLocationId,
        result.ObservedLocationId,
        result.ResultType,
        result.IssueDescription,
        result.IsConfirmed);

    public static IQueryable<ReconciliationSession> IncludeDetails(this IQueryable<ReconciliationSession> sessions) =>
        sessions.Include(session => session.Location).Include(session => session.Results);
}

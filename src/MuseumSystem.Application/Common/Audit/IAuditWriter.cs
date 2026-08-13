namespace MuseumSystem.Application.Common.Audit;

public sealed record AuditWriteRequest(
    string ActionName,
    string ModuleName,
    string EntityName,
    string EntityId,
    string Summary,
    string? ChangeSummary = null);

public interface IAuditWriter
{
    Task<string> WriteAsync(AuditWriteRequest request, CancellationToken cancellationToken = default);
}

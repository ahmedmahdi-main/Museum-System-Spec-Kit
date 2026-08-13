namespace MuseumSystem.Application.Common;

public sealed record ValidationIssue(string Code, string Message, string? Field = null);

public sealed record UseCaseResult(
    bool Succeeded,
    IReadOnlyList<string> Messages,
    IReadOnlyList<ValidationIssue> ValidationIssues,
    bool ConcurrencyConflict = false,
    string? AuditReference = null)
{
    public static UseCaseResult Success(string? message = null, string? auditReference = null) =>
        new(true, message is null ? [] : [message], [], false, auditReference);

    public static UseCaseResult Failure(params ValidationIssue[] issues) =>
        new(false, [], issues);

    public static UseCaseResult Conflict(string message) =>
        new(false, [message], [], true);
}

public sealed record UseCaseResult<T>(
    bool Succeeded,
    T? Value,
    IReadOnlyList<string> Messages,
    IReadOnlyList<ValidationIssue> ValidationIssues,
    bool ConcurrencyConflict = false,
    string? AuditReference = null)
{
    public static UseCaseResult<T> Success(T value, string? message = null, string? auditReference = null) =>
        new(true, value, message is null ? [] : [message], [], false, auditReference);

    public static UseCaseResult<T> Failure(params ValidationIssue[] issues) =>
        new(false, default, [], issues);

    public static UseCaseResult<T> Conflict(string message) =>
        new(false, default, [message], [], true);
}

using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class CancelPhotographyRequestUseCase(
    IMuseumDbContext dbContext,
    IAuditActorContext actorContext,
    ICurrentActorPermissionChecker permissionChecker,
    IAuditWriter auditWriter,
    TimeProvider clock)
{
    public async Task<UseCaseResult<PhotographyRequestDto>> CancelPhotographyRequest(
        CancelPhotographyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = PhotographyRequestUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (actor.Failure is not null)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(actor.Failure);
        }

        if (command.PhotographyRequestId == Guid.Empty)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographyRequest.Required", "Photography request is required.", nameof(command.PhotographyRequestId)));
        }

        if (command.ExpectedConcurrencyToken < 0)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographyRequest.ConcurrencyTokenInvalid", "Expected concurrency token cannot be negative.", nameof(command.ExpectedConcurrencyToken)));
        }

        var request = await dbContext.PhotographyRequests
            .FirstOrDefaultAsync(request => request.PhotographyRequestId == command.PhotographyRequestId, cancellationToken);
        if (request is null)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographyRequest.NotFound", "Photography request was not found.", nameof(command.PhotographyRequestId)));
        }

        if (request.ConcurrencyToken != command.ExpectedConcurrencyToken)
        {
            return PhotographyRequestUseCaseSupport.StaleRequest<PhotographyRequestDto>("cancelling");
        }

        if (request.Status != PhotographyRequestStatus.Pending)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(PhotographyRequestUseCaseSupport.TerminalState("cancelled"));
        }

        var actorHasManageAuthority = permissionChecker.HasPermission(PermissionNames.PhotographyManage);
        if (!actorHasManageAuthority && !string.Equals(actor.UserId, request.RequestedByUserId, StringComparison.Ordinal))
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyManage));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            request.Cancel(actor.UserId!, clock.GetUtcNow(), actorHasManageAuthority);
            await dbContext.SaveChangesAsync(cancellationToken);
            var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
                PhotographyAuditActions.RequestCancel,
                "Photography",
                nameof(PhotographyRequest),
                request.PhotographyRequestId.ToString(),
                "Cancelled photography request.",
                $"ArtifactId={request.ArtifactId}; Purpose={request.Purpose}; CancelledByUserId={request.CancelledByUserId}; Status={request.Status}"),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return UseCaseResult<PhotographyRequestDto>.Success(
                PhotographyRequestQueries.ToRequestDto(request),
                "Photography request cancelled.",
                auditReference);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return PhotographyRequestUseCaseSupport.StaleRequest<PhotographyRequestDto>("cancelling");
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or ArgumentException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographyRequest.CancelInvalid", ex.Message));
        }
    }
}

public sealed record CancelPhotographyRequestCommand(
    Guid PhotographyRequestId,
    int ExpectedConcurrencyToken);

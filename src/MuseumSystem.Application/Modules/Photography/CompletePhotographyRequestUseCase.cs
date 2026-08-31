using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class CompletePhotographyRequestUseCase(
    IMuseumDbContext dbContext,
    IAuditActorContext actorContext,
    ICurrentActorPermissionChecker permissionChecker,
    IAuditWriter auditWriter,
    TimeProvider clock)
{
    public async Task<UseCaseResult<PhotographyRequestDto>> CompletePhotographyRequest(
        CompletePhotographyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = PhotographyRequestUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (actor.Failure is not null)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(actor.Failure);
        }

        if (!permissionChecker.HasPermission(PermissionNames.PhotographyUpload))
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyUpload));
        }

        if (command.PhotographyRequestId == Guid.Empty)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographyRequest.Required", "Photography request is required.", nameof(command.PhotographyRequestId)));
        }

        if (command.FulfillingPhotographySetId == Guid.Empty)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographySet.Required", "Photography set is required.", nameof(command.FulfillingPhotographySetId)));
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
            return PhotographyRequestUseCaseSupport.StaleRequest<PhotographyRequestDto>("completing");
        }

        if (request.Status != PhotographyRequestStatus.Pending)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(PhotographyRequestUseCaseSupport.TerminalState("completed"));
        }

        var set = await dbContext.PhotographySets
            .AsNoTracking()
            .FirstOrDefaultAsync(set => set.PhotographySetId == command.FulfillingPhotographySetId, cancellationToken);
        if (set is null)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographySet.NotFound", "Photography set was not found.", nameof(command.FulfillingPhotographySetId)));
        }

        if (set.ArtifactId != request.ArtifactId)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographySet.ArtifactConflict", "Photography set must belong to the requested artifact.", nameof(command.FulfillingPhotographySetId)));
        }

        if (set.Purpose != request.Purpose)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographySet.PurposeConflict", "Photography set must have the requested purpose.", nameof(command.FulfillingPhotographySetId)));
        }

        var availableImageCount = await dbContext.ArtifactImages
            .AsNoTracking()
            .CountAsync(image =>
                image.PhotographySetId == set.PhotographySetId
                && image.ArtifactId == set.ArtifactId
                && image.Status == ArtifactImageStatus.Available,
                cancellationToken);
        if (availableImageCount == 0)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographySet.AvailableImageRequired", "Fulfilling photography set must contain at least one available image.", nameof(command.FulfillingPhotographySetId)));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            request.Complete(
                set.PhotographySetId,
                set.ArtifactId,
                set.Purpose,
                fulfillingSetHasAvailableImage: true,
                actor.UserId!,
                clock.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
                PhotographyAuditActions.RequestComplete,
                "Photography",
                nameof(PhotographyRequest),
                request.PhotographyRequestId.ToString(),
                "Completed photography request.",
                $"ArtifactId={request.ArtifactId}; Purpose={request.Purpose}; FulfillingPhotographySetId={request.FulfillingPhotographySetId}; CompletedByUserId={request.CompletedByUserId}; Status={request.Status}"),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return UseCaseResult<PhotographyRequestDto>.Success(
                PhotographyRequestQueries.ToRequestDto(request),
                "Photography request completed.",
                auditReference);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return PhotographyRequestUseCaseSupport.StaleRequest<PhotographyRequestDto>("completing");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("PhotographyRequest.CompleteInvalid", ex.Message));
        }
    }
}

public sealed record CompletePhotographyRequestCommand(
    Guid PhotographyRequestId,
    Guid FulfillingPhotographySetId,
    int ExpectedConcurrencyToken);

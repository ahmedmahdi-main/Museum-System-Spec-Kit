using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class CreatePhotographyRequestUseCase(
    IMuseumDbContext dbContext,
    IAuditActorContext actorContext,
    ICurrentActorPermissionChecker permissionChecker,
    IAuditWriter auditWriter,
    TimeProvider clock)
{
    public async Task<UseCaseResult<PhotographyRequestDto>> CreatePhotographyRequest(
        CreatePhotographyRequestCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = PhotographyRequestUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (actor.Failure is not null)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(actor.Failure);
        }

        if (!permissionChecker.HasPermission(PermissionNames.PhotographyRequest))
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyRequest));
        }

        if (command.ArtifactId == Guid.Empty)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("Artifact.Required", "Artifact is required.", nameof(command.ArtifactId)));
        }

        if (!PhotographyRequestUseCaseSupport.IsSupportedPurpose(command.Purpose))
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("Photography.PurposeInvalid", "Photography purpose is not supported.", nameof(command.Purpose)));
        }

        var artifact = await dbContext.Artifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(artifact => artifact.ArtifactId == command.ArtifactId, cancellationToken);
        if (artifact is null)
        {
            return UseCaseResult<PhotographyRequestDto>.Failure(new ValidationIssue("Artifact.NotFound", "Artifact was not found.", nameof(command.ArtifactId)));
        }

        var request = PhotographyRequest.Create(
            command.ArtifactId,
            command.Purpose,
            actor.UserId!,
            clock.GetUtcNow());

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        try
        {
            dbContext.PhotographyRequests.Add(request);
            await dbContext.SaveChangesAsync(cancellationToken);
            var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
                PhotographyAuditActions.RequestCreate,
                "Photography",
                nameof(PhotographyRequest),
                request.PhotographyRequestId.ToString(),
                $"Created photography request for artifact {artifact.MuseumNumberDisplay}.",
                $"ArtifactId={request.ArtifactId}; Purpose={request.Purpose}; RequestedByUserId={request.RequestedByUserId}; Status={request.Status}"),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return UseCaseResult<PhotographyRequestDto>.Success(
                PhotographyRequestQueries.ToRequestDto(request),
                "Photography request created.",
                auditReference);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }
    }
}

public sealed record CreatePhotographyRequestCommand(
    Guid ArtifactId,
    PhotographyPurpose Purpose);

internal static class PhotographyRequestUseCaseSupport
{
    public static TrustedPhotographyRequestActorResolution GetTrustedActorUserId(IAuditActorContext actorContext)
    {
        ArgumentNullException.ThrowIfNull(actorContext);
        var actor = actorContext.CurrentActor;
        return actor.IsAuthenticated && !string.IsNullOrWhiteSpace(actor.UserId)
            ? new TrustedPhotographyRequestActorResolution(actor.UserId.Trim(), null)
            : new TrustedPhotographyRequestActorResolution(null, new ValidationIssue("Photography.ActorRequired", "Authenticated actor is required."));
    }

    public static ValidationIssue PermissionDenied(string permissionName) =>
        new("Photography.PermissionDenied", $"Permission '{permissionName}' is required.");

    public static bool IsSupportedPurpose(PhotographyPurpose purpose) =>
        purpose is PhotographyPurpose.GeneralDocumentation
            or PhotographyPurpose.PreMaintenance
            or PhotographyPurpose.DuringMaintenance
            or PhotographyPurpose.PostMaintenance;

    public static UseCaseResult<T> StaleRequest<T>(string action) =>
        UseCaseResult<T>.Conflict($"Photography request changed. Reload and review the latest request before {action}.");

    public static ValidationIssue TerminalState(string action) =>
        new("PhotographyRequest.NotPending", $"Only pending photography requests can be {action}.");
}

public sealed record TrustedPhotographyRequestActorResolution(string? UserId, ValidationIssue? Failure);

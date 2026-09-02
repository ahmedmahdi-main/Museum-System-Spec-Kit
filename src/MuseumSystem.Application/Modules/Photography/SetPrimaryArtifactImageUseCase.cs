using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common;
using MuseumSystem.Application.Common.Audit;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Application.Modules.IdentityAccess;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class SetPrimaryArtifactImageUseCase(
    IMuseumDbContext dbContext,
    IAuditActorContext actorContext,
    ICurrentActorPermissionChecker permissionChecker,
    IAuditWriter auditWriter,
    ArtifactPhotographyStateService stateService,
    TimeProvider clock)
{
    public async Task<UseCaseResult<PrimaryImageManagementDto>> SetPrimaryArtifactImage(
        SetPrimaryArtifactImageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var actor = PhotographyRequestUseCaseSupport.GetTrustedActorUserId(actorContext);
        if (actor.Failure is not null)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(actor.Failure);
        }

        if (!permissionChecker.HasPermission(PermissionNames.PhotographyManage))
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(PhotographyRequestUseCaseSupport.PermissionDenied(PermissionNames.PhotographyManage));
        }

        if (command.ArtifactId == Guid.Empty)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(new ValidationIssue("Artifact.Required", "Artifact is required.", nameof(command.ArtifactId)));
        }

        if (command.ArtifactImageId == Guid.Empty)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(new ValidationIssue("ArtifactImage.Required", "Artifact image is required.", nameof(command.ArtifactImageId)));
        }

        if (command.ExpectedConcurrencyToken < 0)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(new ValidationIssue("ArtifactPhotographyState.ConcurrencyTokenInvalid", "Expected concurrency token cannot be negative.", nameof(command.ExpectedConcurrencyToken)));
        }

        var artifactExists = await dbContext.Artifacts
            .AsNoTracking()
            .AnyAsync(artifact => artifact.ArtifactId == command.ArtifactId, cancellationToken);
        if (!artifactExists)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(new ValidationIssue("Artifact.NotFound", "Artifact was not found.", nameof(command.ArtifactId)));
        }

        var image = await dbContext.ArtifactImages
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ArtifactImageId == command.ArtifactImageId, cancellationToken);
        if (image is null)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(new ValidationIssue("ArtifactImage.NotFound", "Artifact image was not found.", nameof(command.ArtifactImageId)));
        }

        var targetValidation = stateService.ValidateTargetImage(image, command.ArtifactId);
        if (targetValidation == ArtifactPrimaryImageTargetValidation.ArtifactConflict)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(new ValidationIssue("PrimaryImage.ArtifactConflict", "Primary image must belong to the selected artifact.", nameof(command.ArtifactImageId)));
        }

        if (targetValidation == ArtifactPrimaryImageTargetValidation.NotEligible)
        {
            return UseCaseResult<PrimaryImageManagementDto>.Failure(new ValidationIssue("PrimaryImage.ImageNotEligible", "Only available images can be selected as Primary Image.", nameof(command.ArtifactImageId)));
        }

        await using var transaction = await dbContext.BeginTransactionAsync(cancellationToken);
        ArtifactPhotographyStateMutation? mutation = null;
        try
        {
            mutation = await stateService.SetPrimaryImage(
                command.ArtifactId,
                command.ArtifactImageId,
                command.ExpectedConcurrencyToken,
                actor.UserId!,
                cancellationToken);

            if (mutation.ConcurrencyConflict)
            {
                await transaction.RollbackAsync(cancellationToken);
                dbContext.ClearTrackedChanges();
                return StateConflict();
            }

            if (!mutation.Changed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return UseCaseResult<PrimaryImageManagementDto>.Success(ToManagementDto(mutation.State!, mutation.PreviousPrimaryImageId), "Primary image is already current.");
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            var changedAt = clock.GetUtcNow();
            var auditReference = await auditWriter.WriteAsync(new AuditWriteRequest(
                PhotographyAuditActions.PrimaryImageChange,
                "Photography",
                nameof(ArtifactPhotographyState),
                command.ArtifactId.ToString(),
                "Changed artifact Primary Image.",
                $"ArtifactId={command.ArtifactId}; PreviousPrimaryImageId={FormatAuditValue(mutation.PreviousPrimaryImageId)}; NewPrimaryImageId={command.ArtifactImageId}; ActingUserId={actor.UserId}; ChangedAtUtc={changedAt:O}"),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return UseCaseResult<PrimaryImageManagementDto>.Success(
                ToManagementDto(mutation.State!, mutation.PreviousPrimaryImageId),
                "Primary image updated.",
                auditReference);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            return StateConflict();
        }
        catch (DbUpdateException) when (mutation?.CreatedState == true)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            if (await stateService.AuthoritativeStateExists(command.ArtifactId, cancellationToken))
            {
                return StateConflict();
            }

            throw;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ClearTrackedChanges();
            throw;
        }
    }



    private static UseCaseResult<PrimaryImageManagementDto> StateConflict() =>
        UseCaseResult<PrimaryImageManagementDto>.Conflict("ArtifactPhotographyState.ConcurrencyConflict: Artifact Primary Image changed. Reload and review the latest primary state before editing.");

    private static PrimaryImageManagementDto ToManagementDto(ArtifactPhotographyState state, Guid? previousPrimaryImageId) =>
        new(
            state.ArtifactId,
            state.PrimaryImageId,
            previousPrimaryImageId,
            state.UpdatedAt,
            state.UpdatedByUserId,
            state.ConcurrencyToken);

    private static string FormatAuditValue(Guid? value) => value?.ToString() ?? "<null>";
}

public sealed record SetPrimaryArtifactImageCommand(
    Guid ArtifactId,
    Guid ArtifactImageId,
    int ExpectedConcurrencyToken);

public sealed record PrimaryImageManagementDto(
    Guid ArtifactId,
    Guid? PrimaryImageId,
    Guid? PreviousPrimaryImageId,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByUserId,
    int ConcurrencyToken);

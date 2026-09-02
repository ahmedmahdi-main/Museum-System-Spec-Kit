using Microsoft.EntityFrameworkCore;
using MuseumSystem.Application.Common.Persistence;
using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Application.Modules.Photography;

public sealed class ArtifactPhotographyStateService(IMuseumDbContext dbContext)
{
    public async Task<ArtifactPhotographyStateSnapshot> GetSnapshot(
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        var state = await dbContext.ArtifactPhotographyStates
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ArtifactId == artifactId, cancellationToken);

        return state is null
            ? ArtifactPhotographyStateSnapshot.Missing(artifactId)
            : new ArtifactPhotographyStateSnapshot(
                state.ArtifactId,
                state.PrimaryImageId,
                state.ConcurrencyToken,
                state.UpdatedAt,
                state.UpdatedByUserId,
                Exists: true);
    }

    public async Task<bool> AuthoritativeStateExists(
        Guid artifactId,
        CancellationToken cancellationToken = default) =>
        await dbContext.ArtifactPhotographyStates
            .AsNoTracking()
            .AnyAsync(state => state.ArtifactId == artifactId, cancellationToken);

    public ArtifactPrimaryImageTargetValidation ValidateTargetImage(
        ArtifactImage image,
        Guid artifactId)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.ArtifactId != artifactId)
        {
            return ArtifactPrimaryImageTargetValidation.ArtifactConflict;
        }

        return PhotographyRules.IsPrimaryImageEligible(image, artifactId)
            ? ArtifactPrimaryImageTargetValidation.Eligible
            : ArtifactPrimaryImageTargetValidation.NotEligible;
    }

    public async Task<ArtifactPhotographyStateMutation> SetPrimaryImage(
        Guid artifactId,
        Guid artifactImageId,
        int expectedConcurrencyToken,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var state = await dbContext.ArtifactPhotographyStates
            .FirstOrDefaultAsync(candidate => candidate.ArtifactId == artifactId, cancellationToken);

        if (state is null)
        {
            if (expectedConcurrencyToken != 0)
            {
                return ArtifactPhotographyStateMutation.Conflict(null, createdState: false);
            }

            state = ArtifactPhotographyState.Create(artifactId);
            state.SetPrimaryImage(artifactImageId, actorUserId);
            dbContext.ArtifactPhotographyStates.Add(state);
            return ArtifactPhotographyStateMutation.Success(state, previousPrimaryImageId: null, createdState: true);
        }

        if (state.ConcurrencyToken != expectedConcurrencyToken)
        {
            return ArtifactPhotographyStateMutation.Conflict(state, createdState: false);
        }

        if (state.PrimaryImageId == artifactImageId)
        {
            return ArtifactPhotographyStateMutation.NoOp(state);
        }

        var previousPrimaryImageId = state.PrimaryImageId;
        state.SetPrimaryImage(artifactImageId, actorUserId);
        return ArtifactPhotographyStateMutation.Success(state, previousPrimaryImageId, createdState: false);
    }
}

public sealed record ArtifactPhotographyStateSnapshot(
    Guid ArtifactId,
    Guid? PrimaryImageId,
    int ConcurrencyToken,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByUserId,
    bool Exists)
{
    public static ArtifactPhotographyStateSnapshot Missing(Guid artifactId) =>
        new(artifactId, null, 0, null, null, Exists: false);
}

public enum ArtifactPrimaryImageTargetValidation
{
    Eligible = 1,
    ArtifactConflict = 2,
    NotEligible = 3
}

public sealed record ArtifactPhotographyStateMutation(
    ArtifactPhotographyState? State,
    Guid? PreviousPrimaryImageId,
    bool CreatedState,
    bool Changed,
    bool ConcurrencyConflict)
{
    public static ArtifactPhotographyStateMutation Conflict(ArtifactPhotographyState? state, bool createdState) =>
        new(state, state?.PrimaryImageId, createdState, Changed: false, ConcurrencyConflict: true);

    public static ArtifactPhotographyStateMutation NoOp(ArtifactPhotographyState state) =>
        new(state, state.PrimaryImageId, CreatedState: false, Changed: false, ConcurrencyConflict: false);

    public static ArtifactPhotographyStateMutation Success(ArtifactPhotographyState state, Guid? previousPrimaryImageId, bool createdState) =>
        new(state, previousPrimaryImageId, createdState, Changed: true, ConcurrencyConflict: false);
}

using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public static class CurrentStateRules
{
    public static bool CanDeliver(Artifact artifact) => artifact.CurrentStatus == ArtifactCurrentStatus.InStorage;

    public static bool CanReturn(Artifact artifact) => artifact.CurrentStatus == ArtifactCurrentStatus.OutOfStorage;

    public static bool IsHeldBy(Artifact artifact, MovementRecipientType holderType) =>
        artifact.CurrentStatus == ArtifactCurrentStatus.OutOfStorage &&
        string.Equals(artifact.CurrentHolderType, holderType.ToString(), StringComparison.Ordinal);

    public static bool IsValidStorageLocation(Location location) => location.IsActive && location.LocationType == LocationType.Storage;

    public static bool IsValidDisplayLocation(Location location) => location.IsActive && location.LocationType == LocationType.DisplayHall;

    public static string? GetDeliveryRejectionReason(Artifact artifact) =>
        CanDeliver(artifact) ? null : "القطعة ليست داخل المخزن.";

    public static string? GetReturnRejectionReason(Artifact artifact) =>
        CanReturn(artifact) ? null : "القطعة ليست خارج المخزن.";
}

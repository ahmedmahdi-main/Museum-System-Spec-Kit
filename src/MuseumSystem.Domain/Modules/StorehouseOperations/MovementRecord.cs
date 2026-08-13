using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Domain.Modules.StorehouseOperations;

public sealed class MovementRecord
{
    private MovementRecord()
    {
    }

    private MovementRecord(
        MovementType movementType,
        Guid movementGroupId,
        Guid artifactId,
        MovementRecipientType? recipientType,
        string? recipientName,
        string? purpose,
        Guid? returnLocationId,
        string? note,
        string? recordedBy)
    {
        MovementId = Guid.NewGuid();
        MovementType = movementType;
        MovementGroupId = movementGroupId;
        ArtifactId = artifactId;
        RecipientType = recipientType;
        RecipientName = NormalizeOptional(recipientName);
        Purpose = NormalizeOptional(purpose);
        ReturnLocationId = returnLocationId;
        Note = NormalizeOptional(note);
        OccurredAt = DateTimeOffset.UtcNow;
        RecordedBy = NormalizeOptional(recordedBy);
    }

    public Guid MovementId { get; private set; }
    public MovementType MovementType { get; private set; }
    public Guid MovementGroupId { get; private set; }
    public Guid ArtifactId { get; private set; }
    public Artifact? Artifact { get; private set; }
    public MovementRecipientType? RecipientType { get; private set; }
    public string? RecipientName { get; private set; }
    public string? Purpose { get; private set; }
    public Guid? ReturnLocationId { get; private set; }
    public Location? ReturnLocation { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? RecordedBy { get; private set; }

    public static MovementRecord CreateDelivery(
        Guid movementGroupId,
        Artifact artifact,
        MovementRecipientType recipientType,
        string recipientName,
        string purpose,
        string? note = null,
        string? recordedBy = null) =>
        new(MovementType.Delivery, movementGroupId, artifact.ArtifactId, recipientType, recipientName, purpose, null, note, recordedBy);

    public static MovementRecord CreateReturn(
        Guid movementGroupId,
        Artifact artifact,
        Location returnLocation,
        string? note = null,
        string? recordedBy = null) =>
        new(MovementType.Return, movementGroupId, artifact.ArtifactId, null, null, null, returnLocation.LocationId, note, recordedBy);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

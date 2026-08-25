namespace MuseumSystem.Application.Modules.Photography;

public static class PhotographyAuditActions
{
    public const string ImageUpload = "Photography.Image.Upload";
    public const string ImageMetadataChange = "Photography.Image.MetadataChange";
    public const string PrimaryImageChange = "Photography.PrimaryImage.Change";
    public const string RequestCreate = "Photography.Request.Create";
    public const string RequestComplete = "Photography.Request.Complete";
    public const string RequestCancel = "Photography.Request.Cancel";
    public const string ImageDeleteByUploaderGrace = "Photography.Image.DeleteByUploaderGrace";
    public const string ImageDeletePrivileged = "Photography.Image.DeletePrivileged";
    public const string StorageConsistencyIssue = "Photography.Storage.ConsistencyIssue";
    public const string StorageRecoveryRetry = "Photography.Storage.RecoveryRetry";
    public const string StorageRecoveryResolved = "Photography.Storage.RecoveryResolved";
}

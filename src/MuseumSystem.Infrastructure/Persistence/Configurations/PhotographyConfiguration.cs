using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using static MuseumSystem.Infrastructure.Persistence.Configurations.PhotographyConfigurationExtensions;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class PhotographySetConfiguration : IEntityTypeConfiguration<PhotographySet>
{
    public void Configure(EntityTypeBuilder<PhotographySet> builder)
    {
        builder.ToTable("PhotographySets", table =>
        {
            table.HasCheckConstraint("CK_PhotographySets_Purpose", InConstraint("Purpose", EnumNames<PhotographyPurpose>()));
        });

        builder.HasKey(set => set.PhotographySetId);
        builder.Property(set => set.Purpose).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(set => set.PhotographyDate).HasColumnType("date").IsRequired();
        builder.Property(set => set.PhotographerUserId).HasMaxLength(256).IsRequired();
        builder.Property(set => set.CreatedAt).IsRequired();
        builder.Property(set => set.CreatedByUserId).HasMaxLength(256);
        builder.Property(set => set.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(set => set.ArtifactId);

        builder.HasOne<Artifact>()
            .WithMany()
            .HasForeignKey(set => set.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ArtifactImageConfiguration : IEntityTypeConfiguration<ArtifactImage>
{
    public void Configure(EntityTypeBuilder<ArtifactImage> builder)
    {
        builder.ToTable("ArtifactImages", table =>
        {
            table.HasCheckConstraint("CK_ArtifactImages_FileSizeBytes", "\"FileSizeBytes\" > 0");
            table.HasCheckConstraint("CK_ArtifactImages_PixelWidth", "\"PixelWidth\" > 0");
            table.HasCheckConstraint("CK_ArtifactImages_PixelHeight", "\"PixelHeight\" > 0");
            table.HasCheckConstraint("CK_ArtifactImages_Status", InConstraint("Status", EnumNames<ArtifactImageStatus>()));
            table.HasCheckConstraint("CK_ArtifactImages_DeletionMode", NullableInConstraint("DeletionMode", EnumNames<ArtifactImageDeletionMode>()));
        });

        builder.HasKey(image => image.ArtifactImageId);
        builder.Property(image => image.OriginalObjectKey).HasObjectKeyConversion().HasMaxLength(512).IsRequired();
        builder.Property(image => image.OriginalFilename).HasMaxLength(512).IsRequired();
        builder.Property(image => image.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(image => image.UploadedByUserId).HasMaxLength(256).IsRequired();
        builder.Property(image => image.UploadedAt).IsRequired();
        builder.Property(image => image.Caption).HasMaxLength(1000);
        builder.Property(image => image.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(image => image.DeletedByUserId).HasMaxLength(256);
        builder.Property(image => image.DeletionMode).HasConversion<string>().HasMaxLength(64);
        builder.Property(image => image.DeletionReason).HasMaxLength(1000);
        builder.Property(image => image.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(image => image.ArtifactId);
        builder.HasIndex(image => new { image.PhotographySetId, image.ArtifactId });
        builder.HasIndex(image => image.OriginalObjectKey).IsUnique();
        builder.Metadata.FindNavigation(nameof(ArtifactImage.Derivatives))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Artifact>()
            .WithMany()
            .HasForeignKey(image => image.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PhotographySet>()
            .WithMany()
            .HasForeignKey(image => new { image.PhotographySetId, image.ArtifactId })
            .HasPrincipalKey(set => new { set.PhotographySetId, set.ArtifactId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(image => image.Derivatives)
            .WithOne()
            .HasForeignKey(derivative => derivative.ArtifactImageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ArtifactImageDerivativeConfiguration : IEntityTypeConfiguration<ArtifactImageDerivative>
{
    public void Configure(EntityTypeBuilder<ArtifactImageDerivative> builder)
    {
        builder.ToTable("ArtifactImageDerivatives", table =>
        {
            table.HasCheckConstraint("CK_ArtifactImageDerivatives_Kind", InConstraint("Kind", EnumNames<ImageDerivativeKind>()));
            table.HasCheckConstraint("CK_ArtifactImageDerivatives_FileSizeBytes", "\"FileSizeBytes\" > 0");
            table.HasCheckConstraint("CK_ArtifactImageDerivatives_PixelWidth", "\"PixelWidth\" > 0");
            table.HasCheckConstraint("CK_ArtifactImageDerivatives_PixelHeight", "\"PixelHeight\" > 0");
        });

        builder.HasKey(derivative => derivative.ArtifactImageDerivativeId);
        builder.Property(derivative => derivative.Kind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(derivative => derivative.ObjectKey).HasObjectKeyConversion().HasMaxLength(512).IsRequired();
        builder.Property(derivative => derivative.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(derivative => derivative.CreatedAt).IsRequired();
        builder.HasIndex(derivative => derivative.ArtifactImageId);
        builder.HasIndex(derivative => derivative.ObjectKey).IsUnique();
    }
}

public sealed class ArtifactPhotographyStateConfiguration : IEntityTypeConfiguration<ArtifactPhotographyState>
{
    public void Configure(EntityTypeBuilder<ArtifactPhotographyState> builder)
    {
        builder.ToTable("ArtifactPhotographyStates");
        builder.HasKey(state => state.ArtifactId);
        builder.Property(state => state.UpdatedByUserId).HasMaxLength(256);
        builder.Property(state => state.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(state => state.PrimaryImageId);

        builder.HasOne<Artifact>()
            .WithOne()
            .HasForeignKey<ArtifactPhotographyState>(state => state.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ArtifactImage>()
            .WithMany()
            .HasForeignKey(state => new { state.PrimaryImageId, state.ArtifactId })
            .HasPrincipalKey(image => new { image.ArtifactImageId, image.ArtifactId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PhotographyUploadOperationConfiguration : IEntityTypeConfiguration<PhotographyUploadOperation>
{
    public void Configure(EntityTypeBuilder<PhotographyUploadOperation> builder)
    {
        builder.ToTable("PhotographyUploadOperations", table =>
        {
            table.HasCheckConstraint("CK_PhotographyUploadOperations_OperationKind", InConstraint("OperationKind", EnumNames<PhotographyUploadOperationKind>()));
            table.HasCheckConstraint("CK_PhotographyUploadOperations_Status", InConstraint("Status", EnumNames<PhotographyUploadOperationStatus>()));
        });

        builder.HasKey(operation => operation.PhotographyUploadOperationId);
        builder.Property(operation => operation.ActorUserId).HasMaxLength(256).IsRequired();
        builder.Property(operation => operation.OperationKind).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.Property(operation => operation.RequestFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(operation => operation.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(operation => operation.StartedAt).IsRequired();
        builder.Property(operation => operation.LastSeenAt).IsRequired();
        builder.Property(operation => operation.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(operation => operation.ArtifactId);
        builder.HasIndex(operation => new { operation.PhotographySetId, operation.ArtifactId });
        builder.HasIndex(operation => new { operation.ActorUserId, operation.OperationKind, operation.IdempotencyKey }).IsUnique();
        builder.Metadata.FindNavigation(nameof(PhotographyUploadOperation.FileOutcomes))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<Artifact>()
            .WithMany()
            .HasForeignKey(operation => operation.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PhotographySet>()
            .WithMany()
            .HasForeignKey(operation => new { operation.PhotographySetId, operation.ArtifactId })
            .HasPrincipalKey(set => new { set.PhotographySetId, set.ArtifactId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(operation => operation.FileOutcomes)
            .WithOne()
            .HasForeignKey(outcome => outcome.PhotographyUploadOperationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PhotographyUploadFileOutcomeConfiguration : IEntityTypeConfiguration<PhotographyUploadFileOutcome>
{
    public void Configure(EntityTypeBuilder<PhotographyUploadFileOutcome> builder)
    {
        builder.ToTable("PhotographyUploadFileOutcomes", table =>
        {
            table.HasCheckConstraint("CK_PhotographyUploadFileOutcomes_ClientFileOrdinal", "\"ClientFileOrdinal\" >= 0");
            table.HasCheckConstraint("CK_PhotographyUploadFileOutcomes_Status", InConstraint("Status", EnumNames<PhotographyUploadFileOutcomeStatus>()));
        });

        builder.HasKey(outcome => outcome.PhotographyUploadFileOutcomeId);
        builder.Property(outcome => outcome.OriginalFilename).HasMaxLength(512).IsRequired();
        builder.Property(outcome => outcome.InputFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(outcome => outcome.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(outcome => outcome.OriginalObjectKey).HasNullableObjectKeyConversion().HasMaxLength(512);
        builder.Property(outcome => outcome.DerivativeObjectKeys).HasObjectKeyCollectionConversion().HasColumnType("jsonb").IsRequired();
        builder.Property(outcome => outcome.StaffFacingMessage).HasMaxLength(1000).IsRequired();
        builder.Property(outcome => outcome.CreatedAt).IsRequired();
        builder.HasIndex(outcome => new { outcome.PhotographyUploadOperationId, outcome.ClientFileOrdinal }).IsUnique();
        builder.HasIndex(outcome => outcome.ArtifactImageId);
        builder.HasIndex(outcome => outcome.OriginalObjectKey).IsUnique();

        builder.HasOne<ArtifactImage>()
            .WithMany()
            .HasForeignKey(outcome => outcome.ArtifactImageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StorageOperationRecoveryConfiguration : IEntityTypeConfiguration<StorageOperationRecovery>
{
    public void Configure(EntityTypeBuilder<StorageOperationRecovery> builder)
    {
        builder.ToTable("StorageOperationRecoveries", table =>
        {
            table.HasCheckConstraint("CK_StorageOperationRecoveries_OperationType", InConstraint("OperationType", EnumNames<StorageOperationRecoveryType>()));
            table.HasCheckConstraint("CK_StorageOperationRecoveries_Status", InConstraint("Status", EnumNames<StorageOperationRecoveryStatus>()));
        });

        builder.HasKey(recovery => recovery.StorageOperationRecoveryId);
        builder.Property(recovery => recovery.OperationType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(recovery => recovery.ObjectKeys).HasObjectKeyCollectionConversion().HasColumnType("jsonb").IsRequired();
        builder.Property(recovery => recovery.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(recovery => recovery.FailureSummary).HasMaxLength(1000).IsRequired();
        builder.Property(recovery => recovery.CreatedAt).IsRequired();
        builder.Property(recovery => recovery.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(recovery => recovery.ArtifactId);
        builder.HasIndex(recovery => recovery.ArtifactImageId);

        builder.HasOne<Artifact>()
            .WithMany()
            .HasForeignKey(recovery => recovery.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ArtifactImage>()
            .WithMany()
            .HasForeignKey(recovery => recovery.ArtifactImageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal static class PhotographyConfigurationExtensions
{
    private static readonly ValueComparer<IReadOnlyCollection<ImageStorageObjectKey>> ObjectKeyCollectionComparer =
        new(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            keys => keys.Aggregate(0, (hash, key) => HashCode.Combine(hash, key.Value.GetHashCode(StringComparison.Ordinal))),
            keys => keys.ToArray());

    public static PropertyBuilder<ImageStorageObjectKey> HasObjectKeyConversion(this PropertyBuilder<ImageStorageObjectKey> builder) =>
        builder.HasConversion(key => key.Value, value => ImageStorageObjectKey.Create(value));

    public static PropertyBuilder<ImageStorageObjectKey?> HasNullableObjectKeyConversion(this PropertyBuilder<ImageStorageObjectKey?> builder) =>
        builder.HasConversion(key => key == null ? null : key.Value, value => value == null ? null : ImageStorageObjectKey.Create(value));

    public static PropertyBuilder<IReadOnlyCollection<ImageStorageObjectKey>> HasObjectKeyCollectionConversion(this PropertyBuilder<IReadOnlyCollection<ImageStorageObjectKey>> builder)
    {
        builder.HasConversion(
            keys => JsonSerializer.Serialize(keys.Select(key => key.Value).ToArray(), JsonSerializerOptions.Default),
            value => (JsonSerializer.Deserialize<string[]>(value, JsonSerializerOptions.Default) ?? Array.Empty<string>())
                .Select(ImageStorageObjectKey.Create)
                .ToArray());
        builder.Metadata.SetValueComparer(ObjectKeyCollectionComparer);

        return builder;
    }

    public static string InConstraint(string columnName, IReadOnlyCollection<string> values) =>
        $"\"{columnName}\" IN ({string.Join(", ", values.Select(Quote))})";

    public static string NullableInConstraint(string columnName, IReadOnlyCollection<string> values) =>
        $"\"{columnName}\" IS NULL OR \"{columnName}\" IN ({string.Join(", ", values.Select(Quote))})";

    public static string[] EnumNames<TEnum>() where TEnum : struct, Enum => Enum.GetNames<TEnum>();

    private static string Quote(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}

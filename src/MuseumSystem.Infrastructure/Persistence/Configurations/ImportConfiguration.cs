using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.Import;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches");
        builder.HasKey(batch => batch.ImportBatchId);
        builder.Property(batch => batch.FileName).HasMaxLength(512).IsRequired();
        builder.Property(batch => batch.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(batch => batch.UploadedBy).HasMaxLength(256);
        builder.Property(batch => batch.ValidatedBy).HasMaxLength(256);
        builder.Property(batch => batch.CommittedBy).HasMaxLength(256);
        builder.Property(batch => batch.ConcurrencyToken).IsConcurrencyToken();
        builder.Metadata.FindNavigation(nameof(ImportBatch.Rows))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(batch => batch.Rows)
            .WithOne(row => row.ImportBatch)
            .HasForeignKey(row => row.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("ImportRows");
        builder.HasKey(row => row.ImportRowId);
        builder.Property(row => row.CategoryValue).HasMaxLength(128);
        builder.Property(row => row.ItemNumberValue).HasMaxLength(64);
        builder.Property(row => row.LocationValue).HasMaxLength(256);
        builder.Property(row => row.DescriptionValue).HasMaxLength(2000);
        builder.Property(row => row.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(row => row.Issues).HasMaxLength(2000).IsRequired();
        builder.HasIndex(row => row.ImportBatchId);
    }
}

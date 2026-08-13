using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class MovementRecordConfiguration : IEntityTypeConfiguration<MovementRecord>
{
    public void Configure(EntityTypeBuilder<MovementRecord> builder)
    {
        builder.ToTable("MovementRecords");
        builder.HasKey(record => record.MovementId);
        builder.Property(record => record.MovementType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(record => record.RecipientType).HasConversion<string>().HasMaxLength(64);
        builder.Property(record => record.RecipientName).HasMaxLength(256);
        builder.Property(record => record.Purpose).HasMaxLength(1000);
        builder.Property(record => record.Note).HasMaxLength(1000);
        builder.Property(record => record.RecordedBy).HasMaxLength(256);
        builder.Property(record => record.OccurredAt).IsRequired();
        builder.HasIndex(record => record.ArtifactId);
        builder.HasIndex(record => record.MovementGroupId);

        builder.HasOne(record => record.Artifact)
            .WithMany()
            .HasForeignKey(record => record.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(record => record.ReturnLocation)
            .WithMany()
            .HasForeignKey(record => record.ReturnLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

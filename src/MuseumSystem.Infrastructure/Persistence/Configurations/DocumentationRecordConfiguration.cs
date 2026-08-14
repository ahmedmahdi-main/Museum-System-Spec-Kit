using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class DocumentationRecordConfiguration : IEntityTypeConfiguration<DocumentationRecord>
{
    public void Configure(EntityTypeBuilder<DocumentationRecord> builder)
    {
        builder.ToTable("DocumentationRecords");
        builder.HasKey(record => record.DocumentationRecordId);
        builder.Property(record => record.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(record => record.ValuesJson).HasColumnName("Values").HasColumnType("jsonb").IsRequired();
        builder.Property(record => record.CompletedBaselineValuesJson).HasColumnName("CompletedBaselineValues").HasColumnType("jsonb");
        builder.Property(record => record.CreatedBy).HasMaxLength(256);
        builder.Property(record => record.LastModifiedBy).HasMaxLength(256);
        builder.Property(record => record.CompletedBy).HasMaxLength(256);
        builder.Property(record => record.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(record => record.ArtifactId).IsUnique();
        builder.HasIndex(record => record.DocumentationTemplateVersionId);
        builder.Metadata.FindNavigation(nameof(DocumentationRecord.Revisions))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<MuseumSystem.Domain.Modules.ArtifactRegistry.Artifact>()
            .WithMany()
            .HasForeignKey(record => record.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(record => record.DocumentationTemplateVersion)
            .WithMany()
            .HasForeignKey(record => record.DocumentationTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(record => record.Revisions)
            .WithOne()
            .HasForeignKey(revision => revision.DocumentationRecordId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentationRevisionConfiguration : IEntityTypeConfiguration<DocumentationRevision>
{
    public void Configure(EntityTypeBuilder<DocumentationRevision> builder)
    {
        builder.ToTable("DocumentationRevisions");
        builder.HasKey(revision => revision.DocumentationRevisionId);
        builder.Property(revision => revision.PreviousValuesJson).HasColumnName("PreviousValues").HasColumnType("jsonb").IsRequired();
        builder.Property(revision => revision.NewValuesJson).HasColumnName("NewValues").HasColumnType("jsonb").IsRequired();
        builder.Property(revision => revision.ChangeSummaryJson).HasColumnName("ChangeSummary").HasColumnType("jsonb").IsRequired();
        builder.Property(revision => revision.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(revision => revision.CreatedBy).HasMaxLength(256);
        builder.HasIndex(revision => new { revision.DocumentationRecordId, revision.RevisionNumber }).IsUnique();
        builder.HasIndex(revision => revision.TemplateVersionId);

        builder.HasOne<DocumentationTemplateVersion>()
            .WithMany()
            .HasForeignKey(revision => revision.TemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

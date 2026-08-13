using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class ReconciliationSessionConfiguration : IEntityTypeConfiguration<ReconciliationSession>
{
    public void Configure(EntityTypeBuilder<ReconciliationSession> builder)
    {
        builder.ToTable("ReconciliationSessions");
        builder.HasKey(session => session.ReconciliationSessionId);
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(session => session.Note).HasMaxLength(1000);
        builder.Property(session => session.StartedBy).HasMaxLength(256);
        builder.Property(session => session.CompletedBy).HasMaxLength(256);
        builder.Metadata.FindNavigation(nameof(ReconciliationSession.Results))?.SetPropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne(session => session.Location)
            .WithMany()
            .HasForeignKey(session => session.LocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(session => session.Results)
            .WithOne(result => result.ReconciliationSession)
            .HasForeignKey(result => result.ReconciliationSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ReconciliationResultConfiguration : IEntityTypeConfiguration<ReconciliationResult>
{
    public void Configure(EntityTypeBuilder<ReconciliationResult> builder)
    {
        builder.ToTable("ReconciliationResults");
        builder.HasKey(result => result.ReconciliationResultId);
        builder.Property(result => result.ResultType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(result => result.ObservedMuseumNumber).HasMaxLength(96);
        builder.Property(result => result.IssueDescription).HasMaxLength(1000).IsRequired();
        builder.HasIndex(result => result.ReconciliationSessionId);
        builder.HasIndex(result => result.ArtifactId);
        builder.HasOne(result => result.Artifact)
            .WithMany()
            .HasForeignKey(result => result.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(result => result.ExpectedLocation)
            .WithMany()
            .HasForeignKey(result => result.ExpectedLocationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(result => result.ObservedLocation)
            .WithMany()
            .HasForeignKey(result => result.ObservedLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentedCorrectionConfiguration : IEntityTypeConfiguration<DocumentedCorrection>
{
    public void Configure(EntityTypeBuilder<DocumentedCorrection> builder)
    {
        builder.ToTable("DocumentedCorrections");
        builder.HasKey(correction => correction.CorrectionId);
        builder.Property(correction => correction.SourceType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(correction => correction.CorrectionType).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(correction => correction.PreviousValueSummary).HasMaxLength(2000).IsRequired();
        builder.Property(correction => correction.NewValueSummary).HasMaxLength(2000).IsRequired();
        builder.Property(correction => correction.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(correction => correction.CorrectedBy).HasMaxLength(256);
        builder.HasIndex(correction => correction.ArtifactId);
        builder.HasOne(correction => correction.Artifact)
            .WithMany()
            .HasForeignKey(correction => correction.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

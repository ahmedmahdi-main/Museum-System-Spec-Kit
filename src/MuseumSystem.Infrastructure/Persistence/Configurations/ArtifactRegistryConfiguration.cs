using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.ArtifactRegistry;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class ArtifactCategoryConfiguration : IEntityTypeConfiguration<ArtifactCategory>
{
    public void Configure(EntityTypeBuilder<ArtifactCategory> builder)
    {
        builder.ToTable("ArtifactCategories");
        builder.HasKey(category => category.CategoryId);
        builder.Property(category => category.CategoryCode).HasMaxLength(32).IsRequired();
        builder.HasIndex(category => category.CategoryCode).IsUnique();
        builder.Property(category => category.NameArabic).HasMaxLength(256).IsRequired();
        builder.Property(category => category.Description).HasMaxLength(1000);
    }
}

public sealed class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("Artifacts");
        builder.HasKey(artifact => artifact.ArtifactId);
        builder.Property(artifact => artifact.ItemNumber).IsRequired();
        builder.Property(artifact => artifact.MuseumNumberDisplay).HasMaxLength(96).IsRequired();
        builder.Property(artifact => artifact.BasicDescription).HasMaxLength(2000).IsRequired();
        builder.Property(artifact => artifact.CurrentStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(artifact => artifact.CurrentHolderType).HasMaxLength(128);
        builder.Property(artifact => artifact.CurrentHolderName).HasMaxLength(256);
        builder.Property(artifact => artifact.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(artifact => new { artifact.CategoryId, artifact.ItemNumber }).IsUnique();

        builder.HasOne(artifact => artifact.Category)
            .WithMany()
            .HasForeignKey(artifact => artifact.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(artifact => artifact.CurrentLocation)
            .WithMany()
            .HasForeignKey(artifact => artifact.CurrentLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(artifact => artifact.LastKnownStorageLocation)
            .WithMany()
            .HasForeignKey(artifact => artifact.LastKnownStorageLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

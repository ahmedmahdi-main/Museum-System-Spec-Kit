using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.ArtifactRegistry;
using MuseumSystem.Domain.Modules.Photography;
using static MuseumSystem.Infrastructure.Persistence.Configurations.PhotographyConfigurationExtensions;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class PhotographyRequestConfiguration : IEntityTypeConfiguration<PhotographyRequest>
{
    public void Configure(EntityTypeBuilder<PhotographyRequest> builder)
    {
        builder.ToTable("PhotographyRequests", table =>
        {
            table.HasCheckConstraint("CK_PhotographyRequests_Purpose", InConstraint("Purpose", EnumNames<PhotographyPurpose>()));
            table.HasCheckConstraint("CK_PhotographyRequests_Status", InConstraint("Status", EnumNames<PhotographyRequestStatus>()));
            table.HasCheckConstraint(
                "CK_PhotographyRequests_CompletedMetadata",
                """
                ("Status" = 'Completed' AND "FulfillingPhotographySetId" IS NOT NULL AND "CompletedByUserId" IS NOT NULL AND "CompletedAt" IS NOT NULL AND "CancelledByUserId" IS NULL AND "CancelledAt" IS NULL)
                OR ("Status" <> 'Completed' AND "FulfillingPhotographySetId" IS NULL AND "CompletedByUserId" IS NULL AND "CompletedAt" IS NULL)
                """);
            table.HasCheckConstraint(
                "CK_PhotographyRequests_CancelledMetadata",
                """
                ("Status" = 'Cancelled' AND "CancelledByUserId" IS NOT NULL AND "CancelledAt" IS NOT NULL AND "FulfillingPhotographySetId" IS NULL AND "CompletedByUserId" IS NULL AND "CompletedAt" IS NULL)
                OR ("Status" <> 'Cancelled' AND "CancelledByUserId" IS NULL AND "CancelledAt" IS NULL)
                """);
        });

        builder.HasKey(request => request.PhotographyRequestId);
        builder.Property(request => request.Purpose).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(request => request.RequestedByUserId).HasMaxLength(256).IsRequired();
        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(request => request.CompletedByUserId).HasMaxLength(256);
        builder.Property(request => request.CancelledByUserId).HasMaxLength(256);
        builder.Property(request => request.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(request => request.ArtifactId);
        builder.HasIndex(request => new { request.ArtifactId, request.Status });
        builder.HasIndex(request => request.RequestedByUserId);
        builder.HasIndex(request => new { request.FulfillingPhotographySetId, request.ArtifactId, request.Purpose });

        builder.HasOne<Artifact>()
            .WithMany()
            .HasForeignKey(request => request.ArtifactId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PhotographySet>()
            .WithMany()
            .HasForeignKey(request => new { request.FulfillingPhotographySetId, request.ArtifactId, request.Purpose })
            .HasPrincipalKey(set => new { set.PhotographySetId, set.ArtifactId, set.Purpose })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

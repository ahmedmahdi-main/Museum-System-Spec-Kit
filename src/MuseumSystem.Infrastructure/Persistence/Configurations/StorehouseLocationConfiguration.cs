using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.StorehouseOperations;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class StorehouseLocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("Locations");
        builder.HasKey(location => location.LocationId);
        builder.Property(location => location.NameArabic).HasMaxLength(256).IsRequired();
        builder.Property(location => location.LocationType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(location => new { location.NameArabic, location.LocationType }).IsUnique();

        builder.HasOne(location => location.ParentLocation)
            .WithMany()
            .HasForeignKey(location => location.ParentLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

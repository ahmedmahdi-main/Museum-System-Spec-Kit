using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.IdentityAccess;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.AuditEntryId);
        builder.Property(entry => entry.ActorUserId).HasMaxLength(128);
        builder.Property(entry => entry.ActionName).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.ModuleName).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.EntityName).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.EntityId).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.Summary).HasMaxLength(1000).IsRequired();
        builder.Property(entry => entry.ChangeSummary).HasMaxLength(2000);
        builder.Property(entry => entry.OccurredAt).IsRequired();
        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => new { entry.ModuleName, entry.EntityName, entry.EntityId });
    }
}

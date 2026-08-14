using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuseumSystem.Domain.Modules.Documentation;

namespace MuseumSystem.Infrastructure.Persistence.Configurations;

public sealed class DocumentationTemplateConfiguration : IEntityTypeConfiguration<DocumentationTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentationTemplate> builder)
    {
        builder.ToTable("DocumentationTemplates");
        builder.HasKey(template => template.DocumentationTemplateId);
        builder.Property(template => template.Name).HasMaxLength(256).IsRequired();
        builder.Property(template => template.Description).HasMaxLength(1000);
        builder.Property(template => template.CreatedBy).HasMaxLength(256);
        builder.Property(template => template.LastModifiedBy).HasMaxLength(256);
        builder.HasIndex(template => template.ArtifactCategoryId).IsUnique();
        builder.Metadata.FindNavigation(nameof(DocumentationTemplate.Versions))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<MuseumSystem.Domain.Modules.ArtifactRegistry.ArtifactCategory>()
            .WithMany()
            .HasForeignKey(template => template.ArtifactCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(template => template.Versions)
            .WithOne()
            .HasForeignKey(version => version.DocumentationTemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentationTemplateVersionConfiguration : IEntityTypeConfiguration<DocumentationTemplateVersion>
{
    public void Configure(EntityTypeBuilder<DocumentationTemplateVersion> builder)
    {
        builder.ToTable("DocumentationTemplateVersions");
        builder.HasKey(version => version.DocumentationTemplateVersionId);
        builder.Property(version => version.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(version => version.ActivatedBy).HasMaxLength(256);
        builder.Property(version => version.RetiredBy).HasMaxLength(256);
        builder.Property(version => version.CreatedBy).HasMaxLength(256);
        builder.Property(version => version.LastModifiedBy).HasMaxLength(256);
        builder.Property(version => version.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(version => new { version.DocumentationTemplateId, version.VersionNumber }).IsUnique();
        builder.HasIndex(version => version.DocumentationTemplateId)
            .IsUnique()
            .HasDatabaseName("IX_DocumentationTemplateVersions_OneActivePerTemplate")
            .HasFilter("\"Status\" = 'Active'");
        builder.Metadata.FindNavigation(nameof(DocumentationTemplateVersion.Fields))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(version => version.Fields)
            .WithOne()
            .HasForeignKey(field => field.DocumentationTemplateVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentationTemplateFieldConfiguration : IEntityTypeConfiguration<DocumentationTemplateField>
{
    public void Configure(EntityTypeBuilder<DocumentationTemplateField> builder)
    {
        builder.ToTable("DocumentationTemplateFields");
        builder.HasKey(field => field.DocumentationTemplateFieldId);
        builder.Property(field => field.FieldKey).HasMaxLength(128).IsRequired();
        builder.Property(field => field.Label).HasMaxLength(256).IsRequired();
        builder.Property(field => field.FieldType).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(field => field.Section).HasMaxLength(256).IsRequired();
        builder.Property(field => field.HelpText).HasMaxLength(1000);
        builder.HasIndex(field => new { field.DocumentationTemplateVersionId, field.FieldKey }).IsUnique();
        builder.Metadata.FindNavigation(nameof(DocumentationTemplateField.Options))?.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(field => field.Options)
            .WithOne()
            .HasForeignKey(option => option.DocumentationTemplateFieldId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentationTemplateFieldOptionConfiguration : IEntityTypeConfiguration<DocumentationTemplateFieldOption>
{
    public void Configure(EntityTypeBuilder<DocumentationTemplateFieldOption> builder)
    {
        builder.ToTable("DocumentationTemplateFieldOptions");
        builder.HasKey(option => option.DocumentationTemplateFieldOptionId);
        builder.Property(option => option.OptionKey).HasMaxLength(128).IsRequired();
        builder.Property(option => option.Label).HasMaxLength(256).IsRequired();
        builder.HasIndex(option => new { option.DocumentationTemplateFieldId, option.OptionKey }).IsUnique();
    }
}

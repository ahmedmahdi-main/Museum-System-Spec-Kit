using MuseumSystem.Domain.Modules.Photography;

namespace MuseumSystem.Domain.Tests.Photography;

public sealed class PhotographyPurposeTests
{
    [Fact]
    public void Supported_purposes_match_approved_feature_scope()
    {
        var names = Enum.GetNames<PhotographyPurpose>();

        Assert.Equal([
            "GeneralDocumentation",
            "PreMaintenance",
            "DuringMaintenance",
            "PostMaintenance"
        ], names);
    }

    [Fact]
    public void Photography_types_are_package_and_storage_provider_neutral()
    {
        var domainAssembly = typeof(PhotographyPurpose).Assembly;
        var typeNames = domainAssembly
            .GetTypes()
            .Where(type => type.Namespace == "MuseumSystem.Domain.Modules.Photography")
            .Select(type => type.FullName ?? type.Name)
            .ToArray();

        Assert.DoesNotContain(typeNames, name => name.Contains("Skia", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, name => name.Contains("ImageSharp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, name => name.Contains("Minio", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, name => name.Contains("FileSystem", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, name => name.Contains("Laboratory", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, name => name.Contains("Documentation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, name => name.Contains("Custody", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeNames, name => name.Contains("Movement", StringComparison.OrdinalIgnoreCase));

        var referencedAssemblyNames = domainAssembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(referencedAssemblyNames, name => name.Contains("SkiaSharp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblyNames, name => name.Contains("Minio", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblyNames, name => name.Contains("ImageSharp", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblyNames, name => name.Contains("SixLabors", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblyNames, name => name.Contains("Magick", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Photography_set_keeps_context_without_custody_meaning()
    {
        var artifactId = Guid.NewGuid();
        var set = PhotographySet.Create(
            artifactId,
            PhotographyPurpose.DuringMaintenance,
            new DateOnly(2026, 8, 24),
            "photographer-1",
            "creator-1");

        Assert.Equal(artifactId, set.ArtifactId);
        Assert.Equal(PhotographyPurpose.DuringMaintenance, set.Purpose);
        Assert.Equal(new DateOnly(2026, 8, 24), set.PhotographyDate);
        Assert.Equal("photographer-1", set.PhotographerUserId);
        Assert.Equal("creator-1", set.CreatedByUserId);
    }

    [Fact]
    public void Primary_image_state_is_authoritative_and_nullable()
    {
        var artifactId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var state = ArtifactPhotographyState.Create(artifactId);

        state.SetPrimaryImage(imageId, "manager-1");
        state.ClearPrimaryImage("manager-2");

        Assert.Equal(artifactId, state.ArtifactId);
        Assert.Null(state.PrimaryImageId);
        Assert.Equal(2, state.ConcurrencyToken);
        Assert.Equal("manager-2", state.UpdatedByUserId);
    }

    [Fact]
    public void Deletion_rules_use_exact_sixty_minute_server_boundary()
    {
        var uploadedAt = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);

        Assert.True(PhotographyRules.IsWithinUploaderGracePeriod(uploadedAt, uploadedAt.AddMinutes(60)));
        Assert.False(PhotographyRules.IsWithinUploaderGracePeriod(uploadedAt, uploadedAt.AddMinutes(60).AddTicks(1)));
    }

    [Fact]
    public void Privileged_deletion_reason_is_required_but_grace_reason_is_not()
    {
        Assert.False(PhotographyRules.IsDeletionReasonRequired(ArtifactImageDeletionMode.UploaderGracePeriod));
        Assert.True(PhotographyRules.IsDeletionReasonRequired(ArtifactImageDeletionMode.Privileged));
        Assert.True(PhotographyRules.HasRequiredDeletionReason(ArtifactImageDeletionMode.UploaderGracePeriod, null));
        Assert.False(PhotographyRules.HasRequiredDeletionReason(ArtifactImageDeletionMode.Privileged, " "));
        Assert.True(PhotographyRules.HasRequiredDeletionReason(ArtifactImageDeletionMode.Privileged, "Wrong image uploaded"));
    }

    [Fact]
    public void Undefined_purpose_and_missing_photography_date_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotographySet.Create(
            Guid.NewGuid(),
            (PhotographyPurpose)99,
            new DateOnly(2026, 8, 24),
            "photographer-1"));

        Assert.Throws<ArgumentException>(() => PhotographySet.Create(
            Guid.NewGuid(),
            PhotographyPurpose.GeneralDocumentation,
            default,
            "photographer-1"));
    }

    [Fact]
    public void Undefined_deletion_mode_cannot_bypass_reason_rules()
    {
        var undefinedMode = (ArtifactImageDeletionMode)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => PhotographyRules.IsDeletionReasonRequired(undefinedMode));
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotographyRules.HasRequiredDeletionReason(undefinedMode, null));
    }

    [Fact]
    public void Undefined_upload_operation_kind_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PhotographyUploadOperation.Start(
            "photographer-1",
            (PhotographyUploadOperationKind)99,
            "key-1",
            "fingerprint-1",
            Guid.NewGuid()));
    }
}

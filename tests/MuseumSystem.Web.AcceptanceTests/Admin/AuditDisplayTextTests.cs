using MuseumSystem.Web.Components.Pages.Admin;

namespace MuseumSystem.Web.AcceptanceTests.Admin;

public sealed class AuditDisplayTextTests
{
    [Fact]
    public void Known_audit_values_are_displayed_as_staff_facing_arabic()
    {
        Assert.Equal("التوثيق", AuditDisplayText.Module("Documentation"));
        Assert.Equal("المخزن", AuditDisplayText.Module("StorehouseOperations"));
        Assert.Equal("سجل القطع", AuditDisplayText.Module("ArtifactRegistry"));
        Assert.Equal("إصدار قالب توثيق", AuditDisplayText.Entity("DocumentationTemplateVersion"));
        Assert.Equal("فئة قطعة", AuditDisplayText.Entity("ArtifactCategory"));
        Assert.Equal("موقع خزن", AuditDisplayText.Entity("Location"));
        Assert.Equal("حفظ مسودة إصدار قالب", AuditDisplayText.Action("Documentation.TemplateVersion.SaveDraft"));
        Assert.Equal("إنشاء فئة", AuditDisplayText.Action("ArtifactCategory.Create"));
        Assert.Equal("أنشئ إصدار مسودة فارغ.", AuditDisplayText.Summary("Created empty Draft version."));
        Assert.Equal("حُفظت مسودة إصدار القالب رقم 1.", AuditDisplayText.Summary("Saved Draft template version 1."));
    }

    [Fact]
    public void Unknown_audit_values_fall_back_to_their_original_text()
    {
        Assert.Equal("Unknown.Module", AuditDisplayText.Module("Unknown.Module"));
        Assert.Equal("Unknown.Entity", AuditDisplayText.Entity("Unknown.Entity"));
        Assert.Equal("Unknown.Action", AuditDisplayText.Action("Unknown.Action"));
        Assert.Equal("Developer summary", AuditDisplayText.Summary("Developer summary"));
    }
}

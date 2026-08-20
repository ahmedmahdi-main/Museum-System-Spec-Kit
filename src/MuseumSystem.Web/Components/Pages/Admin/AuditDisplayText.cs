using System.Text.RegularExpressions;

namespace MuseumSystem.Web.Components.Pages.Admin;

public static partial class AuditDisplayText
{
    private static readonly IReadOnlyDictionary<string, string> Modules = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Documentation"] = "التوثيق",
        ["StorehouseOperations"] = "المخزن",
        ["ArtifactRegistry"] = "سجل القطع",
        ["Import"] = "الاستيراد",
        ["IdentityAccess"] = "الصلاحيات",
        ["Audit"] = "التدقيق"
    };

    private static readonly IReadOnlyDictionary<string, string> Entities = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["DocumentationTemplateVersion"] = "إصدار قالب توثيق",
        ["DocumentationTemplate"] = "قالب توثيق",
        ["DocumentationRecord"] = "سجل توثيق",
        ["ArtifactCategory"] = "فئة قطعة",
        ["Artifact"] = "قطعة",
        ["Location"] = "موقع خزن",
        ["MovementRecord"] = "حركة قطعة",
        ["ImportBatch"] = "دفعة استيراد"
    };

    private static readonly IReadOnlyDictionary<string, string> Actions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Documentation.TemplateVersion.SaveDraft"] = "حفظ مسودة إصدار قالب",
        ["Documentation.TemplateVersion.Activate"] = "تفعيل إصدار قالب",
        ["Documentation.TemplateVersion.Retire"] = "إيقاف إصدار قالب",
        ["Documentation.Template.Create"] = "إنشاء قالب توثيق",
        ["Documentation.Record.Create"] = "إنشاء سجل توثيق",
        ["Documentation.Record.SaveDraft"] = "حفظ مسودة سجل توثيق",
        ["Documentation.Record.Complete"] = "إكمال سجل توثيق",
        ["Location.Create"] = "إنشاء موقع",
        ["Location.Update"] = "تحديث موقع",
        ["Location.DisableForNewUse"] = "إيقاف موقع للاستخدام الجديد",
        ["ArtifactCategory.Create"] = "إنشاء فئة",
        ["ArtifactCategory.Update"] = "تحديث فئة",
        ["ArtifactCategory.DisableForNewUse"] = "إيقاف فئة للاستخدام الجديد",
        ["Artifact.Create"] = "إنشاء قطعة",
        ["Artifact.UpdateBasicInfo"] = "تحديث بيانات قطعة",
        ["Import.Commit"] = "اعتماد استيراد"
    };

    private static readonly IReadOnlyDictionary<string, string> ExactSummaries = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Created empty Draft version."] = "أنشئ إصدار مسودة فارغ.",
        ["Category metadata updated."] = "تم تحديث بيانات الفئة.",
        ["BasicDescription updated."] = "تم تحديث الوصف الأساسي.",
        ["No previous Active version existed."] = "لم يكن هناك إصدار نشط سابق.",
        ["Another Active template version remains."] = "يوجد إصدار نشط آخر.",
        ["Category now has zero Active template versions."] = "لم تعد الفئة تملك إصداراً نشطاً."
    };

    public static string Module(string value) => Lookup(Modules, value);

    public static string Entity(string value) => Lookup(Entities, value);

    public static string Action(string value) => Lookup(Actions, value);

    public static string Summary(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        if (ExactSummaries.TryGetValue(value, out var exact))
        {
            return exact;
        }

        var match = CreatedCategoryRegex().Match(value);
        if (match.Success) return $"أنشئت فئة قطعة {match.Groups[1].Value}.";

        match = UpdatedCategoryRegex().Match(value);
        if (match.Success) return $"حُدثت فئة قطعة {match.Groups[1].Value}.";

        match = DisabledCategoryRegex().Match(value);
        if (match.Success) return $"أوقفت فئة قطعة {match.Groups[1].Value} للاستخدام الجديد.";

        match = CreatedArtifactRegex().Match(value);
        if (match.Success) return $"أنشئت قطعة {match.Groups[1].Value}.";

        match = UpdatedArtifactRegex().Match(value);
        if (match.Success) return $"حُدثت البيانات الأساسية للقطعة {match.Groups[1].Value}.";

        match = TemplateVersionRegex().Match(value);
        if (match.Success) return TemplateVersionSummary(match.Groups[1].Value, match.Groups[2].Value);

        match = CreatedTemplateRegex().Match(value);
        if (match.Success) return $"أنشئ قالب توثيق {match.Groups[1].Value} للفئة {match.Groups[2].Value}.";

        match = CopiedVersionRegex().Match(value);
        if (match.Success) return $"نُسخ من الإصدار {match.Groups[1].Value}.";

        match = FieldsRegex().Match(value);
        if (match.Success) return $"عدد الحقول: {match.Groups[1].Value}.";

        match = CreatedLocationRegex().Match(value);
        if (match.Success) return $"أنشئ {LocationType(match.Groups[1].Value)}.";

        match = UpdatedLocationRegex().Match(value);
        if (match.Success) return $"حُدث {LocationType(match.Groups[1].Value)}.";

        match = DisabledLocationRegex().Match(value);
        if (match.Success) return $"أوقف {LocationType(match.Groups[1].Value)} للاستخدام الجديد.";

        return value;
    }

    public static bool HasDisplayValue(string? original, string display) =>
        !string.IsNullOrWhiteSpace(original) && !string.Equals(original, display, StringComparison.Ordinal);

    private static string Lookup(IReadOnlyDictionary<string, string> values, string value) =>
        values.TryGetValue(value, out var display) ? display : value;

    private static string LocationType(string value) => value switch
    {
        "Storage" => "موقع خزن",
        "DisplayHall" => "قاعة عرض",
        _ => value
    };

    private static string TemplateVersionSummary(string verb, string version) => verb switch
    {
        "Created Draft" => $"أنشئ إصدار مسودة رقم {version}.",
        "Saved Draft" => $"حُفظت مسودة إصدار القالب رقم {version}.",
        "Activated" => $"فُعل إصدار القالب رقم {version}.",
        "Retired" => $"أوقف إصدار القالب رقم {version}.",
        _ => $"{verb} {version}."
    };

    [GeneratedRegex(@"^Created artifact category (.+)\.$")]
    private static partial Regex CreatedCategoryRegex();

    [GeneratedRegex(@"^Updated artifact category (.+)\.$")]
    private static partial Regex UpdatedCategoryRegex();

    [GeneratedRegex(@"^Disabled artifact category (.+) for new use\.$")]
    private static partial Regex DisabledCategoryRegex();

    [GeneratedRegex(@"^Created artifact (.+)\.$")]
    private static partial Regex CreatedArtifactRegex();

    [GeneratedRegex(@"^Updated artifact (.+) basic information\.$")]
    private static partial Regex UpdatedArtifactRegex();

    [GeneratedRegex(@"^(Created Draft|Saved Draft|Activated|Retired) template version (\d+)\.$")]
    private static partial Regex TemplateVersionRegex();

    [GeneratedRegex(@"^Created documentation template '(.+)' for category (.+)\.$")]
    private static partial Regex CreatedTemplateRegex();

    [GeneratedRegex(@"^Copied from version (\d+)\.$")]
    private static partial Regex CopiedVersionRegex();

    [GeneratedRegex(@"^Fields=(\d+)$")]
    private static partial Regex FieldsRegex();

    [GeneratedRegex(@"^Created (\w+) location\.$")]
    private static partial Regex CreatedLocationRegex();

    [GeneratedRegex(@"^Updated (\w+) location\.$")]
    private static partial Regex UpdatedLocationRegex();

    [GeneratedRegex(@"^Disabled (\w+) location for new use\.$")]
    private static partial Regex DisabledLocationRegex();
}

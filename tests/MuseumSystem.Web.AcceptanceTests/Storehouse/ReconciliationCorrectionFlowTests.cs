namespace MuseumSystem.Web.AcceptanceTests.Storehouse;

public sealed class ReconciliationCorrectionFlowTests
{
    [Theory]
    [InlineData("Storehouse", "Reconciliation.razor")]
    [InlineData("Storehouse", "DocumentedCorrectionDialog.razor")]
    [InlineData("Admin", "AuditTrail.razor")]
    public void Phase_five_pages_exist(string folder, string fileName)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", folder, fileName);

        Assert.True(File.Exists(path), $"Expected page at {path}");
    }

    [Fact]
    public void Reconciliation_and_correction_pages_show_arabic_operational_copy()
    {
        var root = FindRepositoryRoot();
        var reconciliation = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Storehouse", "Reconciliation.razor"));
        var correction = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Storehouse", "DocumentedCorrectionDialog.razor"));
        var audit = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Admin", "AuditTrail.razor"));

        Assert.Contains("الجرد", reconciliation);
        Assert.Contains("تصحيح موثق", correction);
        Assert.Contains("سبب موثق", correction);
        Assert.Contains("سجل التدقيق", audit);
        Assert.Contains("CreateDocumentedCorrectionUseCase", correction);
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Museum-System.sln")))
        {
            current = current.Parent;
        }

        return current ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}

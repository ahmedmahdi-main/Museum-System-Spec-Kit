namespace MuseumSystem.Web.AcceptanceTests.Storehouse;

public sealed class DeliveryReturnFlowTests
{
    [Theory]
    [InlineData("Delivery.razor")]
    [InlineData("Return.razor")]
    public void Storehouse_movement_pages_exist(string pageName)
    {
        var root = FindRepositoryRoot();
        var path = Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Storehouse", pageName);

        Assert.True(File.Exists(path), $"Expected page at {path}");
    }

    [Fact]
    public void Delivery_and_return_pages_keep_arabic_staff_copy()
    {
        var root = FindRepositoryRoot();
        var deliveryPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Storehouse", "Delivery.razor"));
        var returnPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Storehouse", "Return.razor"));

        Assert.DoesNotContain("stack trace", deliveryPage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", returnPage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@page \"/storehouse/delivery\"", deliveryPage);
        Assert.Contains("@page \"/storehouse/return\"", returnPage);
    }

    [Fact]
    public void Locations_table_has_actions_column_and_localizes_storage_type()
    {
        var root = FindRepositoryRoot();
        var locations = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Storehouse", "Locations.razor"));

        Assert.Contains("<th>الإجراءات</th>", locations);
        Assert.Contains("LocationTypeText", locations);
        Assert.Contains("LocationType.Storage => \"خزن\"", locations);
        Assert.DoesNotContain("<td>@location.LocationType</td>", locations);
        Assert.DoesNotContain("<th></th>", locations);
    }

    [Fact]
    public void Artifact_details_shows_movement_history_panel()
    {
        var root = FindRepositoryRoot();
        var detailsPage = File.ReadAllText(Path.Combine(root.FullName, "src", "MuseumSystem.Web", "Components", "Pages", "Artifacts", "Details.razor"));

        Assert.Contains("MovementHistoryUseCase", detailsPage);
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

using TbotUltra.Worker;
using System.Xml.Linq;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class FarmListsGlobalUiSourceTests
{
    [Fact]
    public void HistoryGrid_KeepsReferenceColumnOrderAndReadableMinimumWidths()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var xaml = XDocument.Load(Path.Combine(
            projectRoot, "src", "TbotUltra.Desktop", "Views", "FarmingPanel.xaml"));
        var grid = xaml.Descendants()
            .Single(element => element.Name.LocalName == "DataGrid"
                && element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Name" && attribute.Value == "FarmHistoryGrid"));
        var columns = grid.Descendants()
            .Where(element => element.Name.LocalName == "DataGridTextColumn")
            .ToList();

        Assert.Equal(
            ["Village", "List", "Sent", "Since previous", "Response", "Source"],
            columns.Select(column => (string?)column.Attribute("Header")));
        Assert.Equal("False", (string?)grid.Attribute("CanUserReorderColumns"));
        Assert.Equal("False", (string?)grid.Attribute("CanUserResizeColumns"));
        Assert.Equal("Auto", (string?)grid.Attribute("ScrollViewer.HorizontalScrollBarVisibility"));
        Assert.All(columns, column => Assert.True(
            double.TryParse((string?)column.Attribute("MinWidth"), out var minimumWidth)
                && minimumWidth >= 95,
            $"{(string?)column.Attribute("Header")} needs a readable minimum width."));
    }

    [Fact]
    public void VillageSelection_DoesNotReplaceAccountWideFarmListRowsOrTimers()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var villageSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.VillageWorking.cs"));
        var farmingSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Farming.FarmLists.cs"));

        var selectionBody = MethodBody(
            villageSource,
            "private void ShowSelectedVillageFromCache",
            "private void ApplySelectedVillageTribeFromCache");

        Assert.DoesNotContain("cached.FarmLists", selectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyCachedFarmListsToUiAsync", selectionBody, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FarmLists = result.Lists",
            farmingSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void NextSendBadge_UsesTheAccountWideFarmQueueInsteadOfTheSelectedVillageCard()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Farming.FarmLists.cs"));

        var body = MethodBody(
            source,
            "private void UpdateNextFarmListSendDisplay",
            "private void WakeContinuousFarmScheduling");

        Assert.Contains("GetQueueSnapshotForUi()", body, StringComparison.Ordinal);
        Assert.Contains("send_farmlists", body, StringComparison.Ordinal);
        Assert.DoesNotContain("_automationLoopTasks", body, StringComparison.Ordinal);
        Assert.DoesNotContain("IsQueueItemForSelectedVillageOrGlobal", body, StringComparison.Ordinal);
    }

    private static string MethodBody(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }
}

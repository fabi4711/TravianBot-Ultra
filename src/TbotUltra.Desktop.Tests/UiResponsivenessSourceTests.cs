using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class UiResponsivenessSourceTests
{
    [Fact]
    public void DashboardPulse_DoesNotRebuildHiddenQueueRows()
    {
        var source = ReadDesktopSource("MainWindow.xaml.cs");
        var pulse = Slice(source, "_clockTimer.Tick +=", "_clockTimer.Start();");

        Assert.Contains("RefreshVillageActivityIndicatorsOnDashboard(refreshQueueRows: _queuePopupWindow?.IsVisible == true)", pulse);
    }

    [Fact]
    public void DemolitionPulse_ReusesUiQueueSnapshot()
    {
        var source = ReadDesktopSource("MainWindow.xaml.cs");
        var pulse = Slice(source, "_clockTimer.Tick +=", "_clockTimer.Start();");
        Assert.Contains("RefreshDemolishStatusForSelectedVillage(useUiSnapshot: true)", pulse);

        var demolition = ReadDesktopSource("MainWindow.Demolish.cs");
        var refresh = Slice(demolition, "private void RefreshDemolishStatusForSelectedVillage", "private static (string Text, bool HasTimer) GetDemolishStatus");
        Assert.Contains("GetQueueSnapshotForUi()", refresh);
    }

    [Fact]
    public void LiveTimerCacheUpdates_UseSerialSnapshotWriter()
    {
        var source = ReadDesktopSource("MainWindow.VillageWorking.cs");
        var selected = Slice(source, "private void UpdateSelectedCachedTimerStatus", "private void UpdateCachedTimerStatus");
        var observed = Slice(source, "private void UpdateCachedTimerStatus", "private void StoreVillageStatusCacheEntry");

        Assert.Contains("_villageCacheWriter.Request(", selected);
        Assert.Contains("_villageCacheWriter.Request(", observed);
        Assert.DoesNotContain("_villageCacheStore.Save(", selected);
        Assert.DoesNotContain("_villageCacheStore.Save(", observed);
    }

    private static string ReadDesktopSource(string fileName)
    {
        var root = ProjectRootLocator.FindProjectRoot();
        return File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", fileName));
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }
}

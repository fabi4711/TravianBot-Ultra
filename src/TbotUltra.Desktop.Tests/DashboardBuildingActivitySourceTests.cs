using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class DashboardBuildingActivitySourceTests
{
    [Fact]
    public void ClockPulse_ReprojectsBuildingActivityWhileDashboardIsVisible()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "TbotUltra.Desktop", "MainWindow.xaml.cs"));
        var tickStart = source.IndexOf("_clockTimer.Tick += (_, _) =>", StringComparison.Ordinal);
        var tickEnd = source.IndexOf("_clockTimer.Start();", tickStart, StringComparison.Ordinal);

        Assert.True(tickStart >= 0 && tickEnd > tickStart);
        var tick = source[tickStart..tickEnd];
        var dashboardGuardStart = tick.IndexOf("if (dashboardTabSelected)", StringComparison.Ordinal);
        var dashboardGuardEnd = tick.IndexOf("// Only render the cached projection here.", dashboardGuardStart, StringComparison.Ordinal);

        Assert.True(dashboardGuardStart >= 0 && dashboardGuardEnd > dashboardGuardStart);
        var dashboardGuard = tick[dashboardGuardStart..dashboardGuardEnd];
        const string dashboardRefresh = "RefreshVillageActivityIndicatorsOnDashboard(refreshQueueRows: _queuePopupWindow?.IsVisible == true)";
        Assert.Contains(dashboardRefresh, dashboardGuard, StringComparison.Ordinal);
        Assert.True(
            tick.IndexOf("TickBuildQueueCountdown();", StringComparison.Ordinal)
            < tick.IndexOf(dashboardRefresh, StringComparison.Ordinal));
    }
}

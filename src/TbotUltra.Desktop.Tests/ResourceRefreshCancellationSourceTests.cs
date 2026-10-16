using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class ResourceRefreshCancellationSourceTests
{
    [Fact]
    public void AutomaticBrowserRefreshes_RequireRunningAutomation()
    {
        var root = FindProjectRoot();
        var resourceSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Resources.Snapshot.cs"));
        var resourceMethod = ExtractMethod(
            resourceSource,
            "private bool ShouldRunBackgroundResourceSnapshotRefresh()",
            "private bool _heroReviveCheckRunning");
        Assert.Contains("!IsContinuousLoopRunning() && !_autoQueueRunning", resourceMethod, StringComparison.Ordinal);

        var inboxSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Inbox.cs"));
        var inboxMethod = ExtractMethod(
            inboxSource,
            "private async Task HandleInboxRefreshTickAsync()",
            "private async Task RefreshInboxIndicatorsQuickAsync()");
        Assert.Contains("!IsContinuousLoopRunning() && !_autoQueueRunning", inboxMethod, StringComparison.Ordinal);
        Assert.Contains("_loopController.AcquireSessionScopeToken()", inboxMethod, StringComparison.Ordinal);
        Assert.Contains("catch (OperationCanceledException) when (sessionToken.IsCancellationRequested)", inboxMethod, StringComparison.Ordinal);

        var antiStarveSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.HeroCropAntiStarve.cs"));
        var antiStarveMethod = ExtractMethod(
            antiStarveSource,
            "private void ActivateDueHeroCropAntiStarveObservations",
            "private bool IsHeroCropAntiStarveEnabled");
        Assert.Contains("!IsContinuousLoopRunning() && !_autoQueueRunning", antiStarveMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpectedSessionCancellation_IsHandledBeforeGenericFailureLogging()
    {
        var root = FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Resources.Snapshot.cs"));
        var start = source.IndexOf("private async Task<VillageStatus?> RefreshResourceSnapshotForUiAsync", StringComparison.Ordinal);
        var end = source.IndexOf("private async Task EnsureTravianLanguageForCurrentPageAsync", start, StringComparison.Ordinal);
        var method = source[start..end];

        var cancellationCatch = method.IndexOf("catch (OperationCanceledException)", StringComparison.Ordinal);
        var genericCatch = method.IndexOf("catch (Exception ex)", StringComparison.Ordinal);
        Assert.True(cancellationCatch >= 0 && cancellationCatch < genericCatch);
    }

    private static string FindProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TbotUltra.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static string ExtractMethod(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }
}

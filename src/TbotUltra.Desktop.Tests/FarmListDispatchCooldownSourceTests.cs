using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class FarmListDispatchCooldownSourceTests
{
    [Fact]
    public void ConfirmedSendPaths_RefreshVisibleHistoryWithoutPolling()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Farming.FarmLists.cs"));

        Assert.Contains("RefreshVisibleFarmListHistoryAsync()",
            MethodBody(source, "private async Task RefreshFarmListsUiAfterAutoSendIfNeededAsync", "private async Task<FarmListsViewResult?> TryApplyFarmListsSnapshotAsync"),
            StringComparison.Ordinal);
        Assert.Contains("RefreshVisibleFarmListHistoryAsync()",
            MethodBody(source, "private async Task FarmListSendNowButtonClickAsync", "private async void FarmListSendAllNowButton_Click"),
            StringComparison.Ordinal);
        Assert.Contains("RefreshVisibleFarmListHistoryAsync()",
            MethodBody(source, "private async Task FarmListSendAllNowButtonClickAsync", "private void SyncFarmingControlsEnabledState"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AutomaticHistoryNotification_IsRaisedImmediatelyAfterPersistence()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var workerSource = File.ReadAllText(Path.Combine(
            projectRoot, "src", "TbotUltra.Worker", "Services", "BotTaskRunner.TaskHandlers.Farming.cs"));
        var uiSource = File.ReadAllText(Path.Combine(
            projectRoot, "src", "TbotUltra.Desktop", "MainWindow.Farming.FarmLists.cs"));
        var savedAt = workerSource.IndexOf("FarmListHistoryStore.Append(", StringComparison.Ordinal);
        var notifiedAt = workerSource.IndexOf("FarmListHistoryUpdated?.Invoke(", StringComparison.Ordinal);
        var rereadAt = workerSource.IndexOf("if (result.AttemptedLists", StringComparison.Ordinal);

        Assert.True(savedAt >= 0 && notifiedAt > savedAt && rereadAt > notifiedAt);
        Assert.Contains("RefreshVisibleFarmListHistoryAsync()",
            MethodBody(uiSource, "private void OnFarmListHistoryUpdated", "private void ClearFarmListHistory"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulDispatchPaths_DoNotClearTheirOwnContinuousCooldown()
    {
        var projectRoot = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "TbotUltra.Desktop",
            "MainWindow.Farming.FarmLists.cs"));

        Assert.DoesNotContain(
            "WakeContinuousFarmScheduling();",
            MethodBody(source, "private async Task RefreshFarmListsUiAfterAutoSendIfNeededAsync", "private async Task<FarmListsViewResult?> TryApplyFarmListsSnapshotAsync"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WakeContinuousFarmScheduling();",
            MethodBody(source, "private async Task FarmListSendNowButtonClickAsync", "private async void FarmListSendAllNowButton_Click"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "WakeContinuousFarmScheduling();",
            MethodBody(source, "private async Task FarmListSendAllNowButtonClickAsync", "private void SyncFarmingControlsEnabledState"),
            StringComparison.Ordinal);
    }

    private static string MethodBody(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return source[start..end];
    }
}

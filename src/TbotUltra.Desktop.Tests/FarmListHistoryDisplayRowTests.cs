using TbotUltra.Core.Farming;
using TbotUltra.Desktop.Views;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class FarmListHistoryDisplayRowTests
{
    [Fact]
    public void FromEntries_GroupsOnlyConfirmedBatchAndPreservesDetails()
    {
        var first = DateTimeOffset.UtcNow.AddMinutes(-2);
        FarmListHistoryEntry[] entries =
        [
            new(first, "A", "1", "GAPP", "Automatic", "success", TimeSpan.FromMinutes(5), "batch-1"),
            new(first.AddSeconds(8), "B", "2", "GAPP", "Automatic", "error", TimeSpan.FromMinutes(6), "batch-1"),
            new(first.AddSeconds(12), "C", "3", "GAPP", "Automatic", "success", null, "batch-2"),
            new(first.AddSeconds(13), "Legacy", "4", "GAPP", "Automatic", "success", null),
        ];

        var rows = FarmListHistoryDisplayRow.FromEntries(entries);

        Assert.Equal(3, rows.Count);
        var batch = Assert.Single(rows, row => row.ListName.StartsWith("2 lists:", StringComparison.Ordinal));
        Assert.Contains("A", batch.ListName);
        Assert.Contains("B", batch.ListName);
        Assert.Equal("GAPP", batch.VillageName);
        Assert.Equal("success+error", batch.Response);
        Assert.Contains("A (GAPP) — success", batch.Details);
        Assert.Contains("B (GAPP) — error", batch.Details);
        Assert.Contains(entries[0].SentAtLocalText, batch.Details);
    }
}

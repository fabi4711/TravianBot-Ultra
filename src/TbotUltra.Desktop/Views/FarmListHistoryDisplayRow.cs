using TbotUltra.Core.Farming;

namespace TbotUltra.Desktop.Views;

public sealed record FarmListHistoryDisplayRow(
    string? VillageName,
    string ListName,
    string SentAtLocalText,
    string SincePreviousText,
    string Response,
    string Origin,
    string Details)
{
    public static IReadOnlyList<FarmListHistoryDisplayRow> FromEntries(IReadOnlyList<FarmListHistoryEntry> entries)
    {
        // Entries without a batch ID predate grouped history; do not guess which send they belonged to.
        return entries
            .Select((entry, index) => (Entry: entry, Key: entry.BatchId ?? $"legacy:{index}"))
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var batch = group.Select(item => item.Entry).OrderBy(item => item.SentAtUtc).ToList();
                var latest = batch[^1];
                var names = batch.Select(item => item.ListName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var villages = batch.Select(item => item.VillageName).Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var intervals = batch.Select(item => item.SincePreviousText).Distinct(StringComparer.Ordinal).ToList();
                var responses = batch.Select(item => item.Response).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var origins = batch.Select(item => item.Origin).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var visibleNames = string.Join(", ", names.Take(3));
                var remainingNames = names.Count - 3;
                var details = string.Join(Environment.NewLine, batch.Select(item =>
                    $"{item.SentAtLocalText} — {item.ListName} ({item.VillageName ?? "Unknown village"}) — {item.Response}; since previous {item.SincePreviousText}"));
                return (Latest: latest.SentAtUtc, Row: new FarmListHistoryDisplayRow(
                    villages.Count == 0 ? null : string.Join(", ", villages),
                    batch.Count == 1 ? latest.ListName : $"{batch.Count} lists: {visibleNames}{(remainingNames > 0 ? $" +{remainingNames} more" : "")}",
                    latest.SentAtLocalText,
                    intervals.Count == 1 ? intervals[0] : $"{intervals[0]}–{intervals[^1]}",
                    string.Join("+", responses),
                    string.Join("+", origins),
                    details));
            })
            .OrderByDescending(item => item.Latest)
            .Select(item => item.Row)
            .ToList();
    }
}

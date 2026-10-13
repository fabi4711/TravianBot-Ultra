using System.Text.Json;

namespace TbotUltra.Core.Configuration;

internal static class VillageNameListPayloadCodec
{
    internal static string Serialize(IEnumerable<string> names)
        => JsonSerializer.Serialize(Normalize(names));

    internal static List<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var names = JsonSerializer.Deserialize<List<string?>>(trimmed);
                if (names is not null)
                {
                    return Normalize(names);
                }
            }
            catch (JsonException)
            {
                // Fall back to the legacy comma-separated payload format below.
            }
        }

        return Normalize(trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    internal static List<string> Normalize(IEnumerable<string?> names)
        => names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}

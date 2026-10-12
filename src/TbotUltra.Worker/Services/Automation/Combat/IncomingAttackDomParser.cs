using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

internal enum IncomingAttackFilterAction
{
    EnableIncomingCategory,
    EnableIncomingSubfilter,
    DisableReinforcementsSubfilter,
    DisableReturningSubfilter,
    Verified,
}

internal static class IncomingAttackDomParser
{
    internal sealed record Dorf1MovementCountdown(bool HasMovements, int? NextSeconds);

    private static readonly Regex VillageRowRegex = new(
        @"<div\b(?<attrs>[^>]*\bclass\s*=\s*[\""'](?=[^\""']*\blistEntry\b)(?=[^\""']*\bvillage\b)(?=[^\""']*\battack\b)[^\""']*[\""'][^>]*)>(?<body>.*?)(?=<div\b[^>]*\bclass\s*=\s*[\""'][^\""']*\blistEntry\b|</div>\s*</div>\s*</div>)",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MovementTableRegex = new(
        @"<table\b(?<attrs>[^>]*\bclass\s*=\s*[\""'][^\""']*\btroop_details\b[^\""']*[\""'][^>]*)>(?<body>.*?)</table>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Dorf1MovementTableRegex = new(
        @"<div\b[^>]*\bclass\s*=\s*[\""'][^\""']*\bvillageInfobox\b[^\""']*\bmovements\b[^\""']*[\""'][^>]*>[\s\S]*?<table\b[^>]*\bid\s*=\s*[\""']movements[\""'][^>]*>(?<body>.*?)</table>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex Dorf1UnitsTableRegex = new(
        @"<div\b[^>]*\bclass\s*=\s*[\""'][^\""']*\bvillageInfobox\b[^\""']*\bunits\b[^\""']*[\""'][^>]*>[\s\S]*?<table\b[^>]*\bid\s*=\s*[\""']troops[\""'][^>]*>(?<body>.*?)</table>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    internal static Dorf1MovementCountdown ParseDorf1MovementCountdown(string? html)
    {
        var table = Dorf1MovementTableRegex.Match(html ?? string.Empty);
        if (!table.Success)
            return new Dorf1MovementCountdown(false, null);

        var nextSeconds = int.MaxValue;
        var hasMovements = false;
        foreach (Match row in Regex.Matches(table.Groups["body"].Value, @"<tr\b[^>]*>(?<body>.*?)</tr>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var body = row.Groups["body"].Value;
            if (!Regex.IsMatch(body, @"<td\b[^>]*\bclass\s*=\s*[\""'][^\""']*\btyp\b", RegexOptions.IgnoreCase))
                continue;

            hasMovements = true;
            var timer = Regex.Match(body,
                @"<span\b[^>]*\bclass\s*=\s*[\""'][^\""']*\btimer\b[^\""']*[\""'][^>]*>(?<time>[^<]*)</span>",
                RegexOptions.IgnoreCase);
            var timerText = WebUtility.HtmlDecode(timer.Groups["time"].Value).Trim();
            var remaining = TimeSpan.TryParse(timerText, CultureInfo.InvariantCulture, out var liveTime)
                && liveTime >= TimeSpan.Zero
                ? (int?)Math.Ceiling(liveTime.TotalSeconds)
                : ParseRemainingSeconds(body);
            if (remaining is null)
                return new Dorf1MovementCountdown(true, null);
            nextSeconds = Math.Min(nextSeconds, remaining.Value);
        }

        return hasMovements
            ? new Dorf1MovementCountdown(true, nextSeconds)
            : new Dorf1MovementCountdown(false, null);
    }

    internal static bool ShouldParkOnDorf2(
        Dorf1MovementCountdown countdown,
        DateTimeOffset? nextTaskAtUtc,
        DateTimeOffset nowUtc)
    {
        if (!countdown.HasMovements)
            return false;
        if (countdown.NextSeconds is null || nextTaskAtUtc is null)
            return true;

        return countdown.NextSeconds.Value < (nextTaskAtUtc.Value - nowUtc).TotalSeconds + 20;
    }

    internal static bool? ParseDorf1HasTroopsAtHome(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var table = Dorf1UnitsTableRegex.Match(html);
        if (!table.Success) return null;
        return !Regex.IsMatch(
            table.Groups["body"].Value,
            @"<td\b[^>]*\bclass\s*=\s*[\""'][^\""']*\bnoTroops\b[^\""']*[\""']",
            RegexOptions.IgnoreCase);
    }

    internal static IReadOnlyList<IncomingAttackSignal> ParseDorf1Signals(
        string? html,
        string activeVillageName,
        string? activeVillageUrl,
        int? activeCoordX,
        int? activeCoordY,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var signals = new List<IncomingAttackSignal>();
        var movementTable = Dorf1MovementTableRegex.Match(html);
        if (movementTable.Success
            && Regex.IsMatch(
                movementTable.Groups["body"].Value,
                @"<img\b[^>]*\bclass\s*=\s*[\""'][^\""']*\batt1\b[^\""']*[\""']",
                RegexOptions.IgnoreCase))
        {
            var arrivals = ParseDorf1RedArrivalTimes(movementTable.Groups["body"].Value, observedAtUtc);
            signals.Add(new IncomingAttackSignal(
                activeVillageName,
                activeVillageUrl,
                TravianUrls.TryParseNewdid(activeVillageUrl),
                activeCoordX,
                activeCoordY,
                observedAtUtc,
                arrivals));
        }

        foreach (Match row in VillageRowRegex.Matches(html))
        {
            var attrs = row.Groups["attrs"].Value;
            var body = row.Groups["body"].Value;
            var villageId = ParseIntAttribute(attrs, "data-did");
            var name = ReadFirstGroup(body, @"<span\b[^>]*\bclass\s*=\s*[\""'][^\""']*\bname\b[^\""']*[\""'][^>]*>(?<value>.*?)</span>");
            var coords = ParseCoordinates(body);
            if (string.IsNullOrWhiteSpace(name) && !villageId.HasValue)
            {
                continue;
            }

            signals.Add(new IncomingAttackSignal(
                name ?? activeVillageName,
                villageId is int did ? $"dorf1.php?newdid={did}" : null,
                villageId,
                coords.X,
                coords.Y,
                observedAtUtc));
        }

        return signals
            .GroupBy(signal => signal.VillageId?.ToString(CultureInfo.InvariantCulture)
                               ?? (signal.CoordX.HasValue && signal.CoordY.HasValue
                                   ? $"{signal.CoordX}|{signal.CoordY}"
                                   : signal.VillageName),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    internal static bool HasPlusVillageOverview(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return false;
        return Regex.IsMatch(
            html,
            @"<div\b(?=[^>]*\bclass\s*=\s*[\""'](?=[^\""']*\blistEntry\b)(?=[^\""']*\bvillage\b)[^\""']*[\""'])(?=[^>]*\bdata-did\s*=\s*[\""']\d+[\""'])[^>]*>",
            RegexOptions.IgnoreCase);
    }

    internal static IReadOnlyList<DateTimeOffset> ParseDorf1RedArrivalTimes(string? html, DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(html)) return [];
        var arrivals = new List<DateTimeOffset>();
        foreach (Match row in Regex.Matches(html, @"<tr\b[^>]*>(?<body>.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var body = row.Groups["body"].Value;
            if (!Regex.IsMatch(body, @"<img\b[^>]*\bclass\s*=\s*[\""'](?=[^\""']*\batt1\b)[^\""']*[\""']", RegexOptions.IgnoreCase))
            {
                continue;
            }
            var seconds = ParseRemainingSeconds(body);
            if (seconds.HasValue) arrivals.Add(observedAtUtc.AddSeconds(Math.Max(0, seconds.Value)));
        }
        return arrivals.Order().ToList();
    }

    internal static IReadOnlyList<IncomingAttack> ParseIncomingAttacks(
        string? html,
        string targetVillageName,
        string? targetVillageKey,
        int? targetCoordX,
        int? targetCoordY,
        DateTimeOffset observedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var attacks = new List<IncomingAttack>();
        foreach (Match table in MovementTableRegex.Matches(html))
        {
            var attrs = table.Groups["attrs"].Value;
            var body = table.Groups["body"].Value;
            var remaining = ParseRemainingSeconds(body);
            if (!remaining.HasValue)
            {
                continue;
            }

            var arrival = observedAtUtc.AddSeconds(Math.Max(0, remaining.Value));
            var id = ReadFirstGroup(body, @"markSymbol_(?<value>\d+)")
                     ?? ReadFirstGroup(body, @"markAttackSymbol\((?<value>\d+)\)");
            var sourceVillage = ReadFirstGroup(body, @"<td\b[^>]*\bclass\s*=\s*[\""'][^\""']*\brole\b[^\""']*[\""'][^>]*>[\s\S]*?<a\b[^>]*>(?<value>.*?)</a>");
            var movementHeadline = ReadFirstGroup(body, @"<td\b[^>]*\bclass\s*=\s*[\""'][^\""']*\btroopHeadline\b[^\""']*[\""'][^>]*>[\s\S]*?<a\b(?![^>]*\bmarkAttack\b)[^>]*>(?<value>.*?)</a>");
            var sourcePlayer = ParseSourcePlayerName(movementHeadline, targetVillageName);
            var sourceCoords = ParseCoordinates(body);
            var movementType = HasCssClass(attrs, "inRaid")
                ? IncomingAttackMovementType.Raid
                : HasCssClass(attrs, "inAttack")
                    ? IncomingAttackMovementType.Attack
                    : IncomingAttackMovementType.Unknown;

            id ??= CreateFallbackId(targetVillageKey, targetVillageName, sourcePlayer, sourceVillage, movementType, arrival);
            attacks.Add(new IncomingAttack(
                id,
                targetVillageName,
                arrival,
                movementType,
                targetVillageKey,
                targetCoordX,
                targetCoordY,
                sourcePlayer,
                sourceVillage,
                sourceCoords.X,
                sourceCoords.Y,
                observedAtUtc));
        }

        return attacks.OrderBy(attack => attack.ArrivalAtUtc).ToList();
    }

    internal static bool HasOnlyIncomingFilterActive(string? html)
    {
        return GetRequiredFilterAction(html) == IncomingAttackFilterAction.Verified;
    }

    internal static IncomingAttackFilterAction GetRequiredFilterAction(string? html)
    {
        if (!IsFilterActive(html, "filterCategory1"))
            return IncomingAttackFilterAction.EnableIncomingCategory;
        if (!IsFilterActive(html, "subFilterCategory1"))
            return IncomingAttackFilterAction.EnableIncomingSubfilter;
        if (IsFilterActive(html, "subFilterCategory2"))
            return IncomingAttackFilterAction.DisableReinforcementsSubfilter;
        if (IsFilterActive(html, "subFilterCategory3"))
            return IncomingAttackFilterAction.DisableReturningSubfilter;
        return IncomingAttackFilterAction.Verified;
    }

    private static bool IsFilterActive(string? html, string imageClass)
    {
        if (string.IsNullOrWhiteSpace(html)) return false;
        return Regex.Matches(
                html,
                @"<button\b[^>]*\bclass\s*=\s*[\""'][^\""']*\biconFilterActive\b[^\""']*[\""'][^>]*>(?<body>.*?)</button>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Cast<Match>()
            .Any(button => Regex.IsMatch(
                button.Groups["body"].Value,
                $@"<img\b[^>]*\bclass\s*=\s*[\""'][^\""']*\b{Regex.Escape(imageClass)}\b[^\""']*[\""']",
                RegexOptions.IgnoreCase));
    }

    private static int? ParseRemainingSeconds(string html)
    {
        var raw = ReadFirstGroup(html, @"<span\b[^>]*(?:data-value|value)\s*=\s*[\""'](?<value>\d+)[\""'][^>]*\bclass\s*=\s*[\""'][^\""']*\btimer\b")
                  ?? ReadFirstGroup(html, @"<span\b[^>]*\bclass\s*=\s*[\""'][^\""']*\btimer\b[^\""']*[\""'][^>]*(?:data-value|value)\s*=\s*[\""'](?<value>\d+)[\""']");
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static (int? X, int? Y) ParseCoordinates(string html)
    {
        var normalizedHtml = Regex.Replace(html, @"[\u202A-\u202E\u2066-\u2069]", string.Empty)
            .Replace('\u2212', '-')
            .Replace('\u2012', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-');
        var x = ReadFirstGroup(normalizedHtml, @"coordinateX[^>]*>[^\d-]*(?<value>-?\d+)");
        var y = ReadFirstGroup(normalizedHtml, @"coordinateY[^>]*>[^\d-]*(?<value>-?\d+)");
        return (
            int.TryParse(x, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedX) ? parsedX : null,
            int.TryParse(y, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedY) ? parsedY : null);
    }

    private static int? ParseIntAttribute(string attrs, string attribute)
    {
        var value = ReadFirstGroup(attrs, $@"\b{Regex.Escape(attribute)}\s*=\s*[\""'](?<value>\d+)[\""']");
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static string? ReadFirstGroup(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return null;
        }

        var text = WebUtility.HtmlDecode(Regex.Replace(match.Groups["value"].Value, "<[^>]+>", " "));
        text = Regex.Replace(text, @"[\u202A-\u202E\u2066-\u2069]", string.Empty);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ParseSourcePlayerName(string? movementHeadline, string targetVillageName)
    {
        if (string.IsNullOrWhiteSpace(movementHeadline))
        {
            return null;
        }

        var movementSuffix = $@"\s+(?:raids|attacks)\s+{Regex.Escape(targetVillageName)}\s*$";
        var playerName = Regex.Replace(movementHeadline, movementSuffix, string.Empty, RegexOptions.IgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(playerName) ? movementHeadline : playerName;
    }

    private static bool HasCssClass(string attrs, string className) =>
        Regex.IsMatch(attrs, $@"\bclass\s*=\s*[\""'][^\""']*\b{Regex.Escape(className)}\b", RegexOptions.IgnoreCase);

    private static string CreateFallbackId(
        string? targetVillageKey,
        string targetVillageName,
        string? sourcePlayer,
        string? sourceVillage,
        IncomingAttackMovementType movementType,
        DateTimeOffset arrival)
    {
        var raw = string.Join('|', targetVillageKey ?? targetVillageName, sourcePlayer, sourceVillage, movementType, arrival.UtcTicks);
        return $"fallback:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..20]}";
    }
}

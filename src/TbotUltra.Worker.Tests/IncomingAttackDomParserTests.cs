using TbotUltra.Worker.Domain;
using TbotUltra.Worker.Services;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class IncomingAttackDomParserTests
{
    [Fact]
    public void Dorf1Parking_UsesEarliestIncomingOrOutgoingMovement()
    {
        const string html = """
            <div class="villageInfobox movements"><table id="movements"><tbody>
              <tr><th>Incoming troops:</th></tr>
              <tr><td class="typ"><img class="def1"></td><td><span class="timer" value="50">0:00:50</span></td></tr>
              <tr><th>Outgoing troops:</th></tr>
              <tr><td class="typ"><img class="att2"></td><td><span class="timer" data-value="50">0:00:40</span></td></tr>
            </tbody></table></div>
            """;
        var countdown = IncomingAttackDomParser.ParseDorf1MovementCountdown(html);
        var now = new DateTimeOffset(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

        Assert.True(countdown.HasMovements);
        Assert.Equal(40, countdown.NextSeconds);
        Assert.False(IncomingAttackDomParser.ShouldParkOnDorf2(countdown, now.AddSeconds(10), now));
        Assert.False(IncomingAttackDomParser.ShouldParkOnDorf2(countdown, now.AddSeconds(20), now));
        Assert.True(IncomingAttackDomParser.ShouldParkOnDorf2(countdown, now.AddSeconds(21), now));
        Assert.True(IncomingAttackDomParser.ShouldParkOnDorf2(countdown, null, now));
    }

    [Fact]
    public void Dorf1Parking_StaysWhenNoMovementsExist()
    {
        var countdown = IncomingAttackDomParser.ParseDorf1MovementCountdown(
            "<div class=\"villageInfobox movements\"><table id=\"movements\"><tr><th>Incoming troops:</th></tr></table></div>");

        Assert.False(countdown.HasMovements);
        Assert.False(IncomingAttackDomParser.ShouldParkOnDorf2(countdown, null, DateTimeOffset.UtcNow));
        Assert.False(IncomingAttackDomParser.ParseDorf1MovementCountdown("<div id=\"villageName\"></div>").HasMovements);
    }

    [Fact]
    public void Dorf1Parking_ParksWhenMovementTimerIsUnreadable()
    {
        var countdown = IncomingAttackDomParser.ParseDorf1MovementCountdown(
            "<div class=\"villageInfobox movements\"><table id=\"movements\"><tr><td class=\"typ\"><img class=\"att1\"></td><td><span class=\"timer\">loading</span></td></tr></table></div>");

        Assert.True(countdown.HasMovements);
        Assert.Null(countdown.NextSeconds);
        Assert.True(IncomingAttackDomParser.ShouldParkOnDorf2(countdown, DateTimeOffset.UtcNow.AddSeconds(1), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CurrentPageSignalRead_UsesPlusOverviewOutsideDorf1WithoutClaimingAuthoritativeDorf1Read()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Combat",
            "TravianClient.IncomingAttacks.cs"));
        var snapshotSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Resources",
            "TravianClient.Resources.Snapshot.cs"));

        Assert.Contains("if (!isDorf1 && _cachedTravianPlusActive != true)", source, StringComparison.Ordinal);
        Assert.Contains("IncomingAttackActiveVillageReadWasAuthoritative: incomingAttackActiveVillageReadWasAuthoritative", snapshotSource, StringComparison.Ordinal);
        Assert.Contains("IncomingAttackPlusOverviewWasRead: incomingAttackSignalRead?.PlusOverviewWasRead == true", snapshotSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RallyPointRead_FollowsEveryVisibleNextPageAndCombinesMovements()
    {
        var root = ProjectRootLocator.FindProjectRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "TbotUltra.Worker",
            "Services",
            "Automation",
            "Combat",
            "TravianClient.IncomingAttacks.cs"));

        Assert.Contains("ReadAllIncomingAttackPagesAsync", source, StringComparison.Ordinal);
        Assert.Contains(".paginatorTop .paginator a.next:has(img[alt='next page'])", source, StringComparison.Ordinal);
        Assert.Contains("incoming-attacks-next-page", source, StringComparison.Ordinal);
        Assert.Contains("attacksById.TryAdd", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseDorf1HasTroopsAtHome_ReadsNoTroopsAsAuthoritativeEmpty()
    {
        const string html = """
            <div class="villageInfobox units">
              <table id="troops"><tbody><tr><td class="noTroops">none</td></tr></tbody></table>
            </div>
            """;

        Assert.False(IncomingAttackDomParser.ParseDorf1HasTroopsAtHome(html));
    }

    [Fact]
    public void ParseDorf1HasTroopsAtHome_DistinguishesPresentFromUnread()
    {
        const string withTroops = """
            <div class="villageInfobox units">
              <table id="troops"><tbody><tr><td class="unit"><img class="unit u61"></td><td class="num">12</td></tr></tbody></table>
            </div>
            """;

        Assert.True(IncomingAttackDomParser.ParseDorf1HasTroopsAtHome(withTroops));
        Assert.Null(IncomingAttackDomParser.ParseDorf1HasTroopsAtHome("<main>not dorf1 units</main>"));
    }

    [Fact]
    public void ParseDorf1Signals_DetectsActiveAndPlusOverviewVillages()
    {
        const string html = """
            <div class="villageInfobox movements"><table id="movements"><tr><td><img class="att1"></td><td>1 Attack</td></tr></table></div>
            <div class="listEntry village attack" data-did="39762">
              <span class="name" data-did="39762">A4</span>
              <span class="coordinateX">(122</span><span class="coordinateY">15)</span>
            </div>
            <div class="listEntry village" data-did="37166"><span class="name">A1</span></div>
            """;
        var observedAt = new DateTimeOffset(2026, 8, 22, 9, 0, 0, TimeSpan.Zero);

        var signals = IncomingAttackDomParser.ParseDorf1Signals(
            html, "SWOLL", "dorf1.php?newdid=34875", 120, 14, observedAt);

        Assert.Equal(2, signals.Count);
        Assert.Contains(signals, signal => signal.VillageName == "SWOLL" && signal.VillageId == 34875);
        Assert.Contains(signals, signal => signal.VillageName == "A4" && signal.VillageId == 39762
                                                   && signal.CoordX == 122 && signal.CoordY == 15);
        Assert.True(IncomingAttackDomParser.HasPlusVillageOverview(html));
    }

    [Fact]
    public void HasPlusVillageOverview_RequiresAStableVillageRow()
    {
        Assert.False(IncomingAttackDomParser.HasPlusVillageOverview("<main>loading</main>"));
        Assert.False(IncomingAttackDomParser.HasPlusVillageOverview("<div class=\"listEntry village\">missing id</div>"));
    }

    [Fact]
    public void ParseDorf1Signals_IgnoresReturningReinforcementsAndOutgoingAttacks()
    {
        const string html = """
            <div class="villageInfobox movements">
              <table id="movements" cellspacing="1" cellpadding="1">
                <tbody>
                  <tr><th class="troopMovements header" colspan="3">Incoming troops:</th></tr>
                  <tr>
                    <td class="typ"><a href="/build.php?gid=16&amp;tt=1&amp;filter=1&amp;subfilters=2,3"><img class="def1" src="/img/x.gif"></a></td>
                    <td><div class="mov"><span class="d1">3 Reinf.</span></div><div class="dur_r">in <span class="timer" value="1044">0:17:24</span> hrs.</div></td>
                  </tr>
                  <tr><th class="troopMovements header" colspan="3">Outgoing troops:</th></tr>
                  <tr>
                    <td class="typ"><a href="/build.php?gid=16&amp;tt=1&amp;filter=2&amp;subfilters=4"><img class="att2" src="/img/x.gif"></a></td>
                    <td><div class="mov"><span class="a2">1 Attack</span></div><div class="dur_r">in <span class="timer" value="444">0:07:24</span> hrs.</div></td>
                  </tr>
                </tbody>
              </table>
            </div>
            """;

        var signals = IncomingAttackDomParser.ParseDorf1Signals(
            html,
            "BRO",
            "dorf1.php?newdid=34875",
            120,
            14,
            new DateTimeOffset(2026, 8, 22, 14, 29, 29, TimeSpan.Zero));

        Assert.Empty(signals);
    }

    [Fact]
    public void ParseDorf1Signals_CapturesOnlyRedAttackFallbackTimers()
    {
        const string html = """
            <div class="villageInfobox movements"><table id="movements">
              <tr><td><img class="def1"></td><td><span class="timer" value="20">0:00:20</span></td></tr>
              <tr><td><img class="att2"></td><td><span class="timer" value="30">0:00:30</span></td></tr>
              <tr><td><img class="att1"></td><td><span class="timer" data-value="142">0:02:22</span></td></tr>
              <tr><td><img class="att1"></td><td><span class="timer" value="300">0:05:00</span></td></tr>
            </table></div>
            """;
        var observed = new DateTimeOffset(2026, 8, 22, 14, 0, 0, TimeSpan.Zero);

        var signal = Assert.Single(IncomingAttackDomParser.ParseDorf1Signals(html, "BRE", "dorf1.php?newdid=1", 1, 2, observed));

        Assert.Equal([observed.AddSeconds(142), observed.AddSeconds(300)], signal.Dorf1ArrivalTimesUtc);
    }

    [Fact]
    public void ParseDorf1Signals_DetectsOnlyVillageRowMarkedAsAttack()
    {
        const string html = """
            <div class="villageList">
              <div class="dropContainer" data-sortid="village22087">
                <div class="listEntry village" data-did="22087">
                  <a href="#"><div class="iconAndNameWrapper"><span class="incomingTroops"><svg class="attack"></svg></span><span class="name" data-did="22087">ABC</span></div></a>
                  <span class="coordinatesGrid"><span class="coordinateX">(61</span>|<span class="coordinateY">22)</span></span>
                </div>
              </div>
              <div class="dropContainer" data-sortid="village32735">
                <div class="listEntry village active attack" data-did="32735">
                  <a href="#" class="active"><div class="iconAndNameWrapper"><span class="incomingTroops"><svg class="attack"></svg></span><span class="name" data-did="32735">BRE</span></div></a>
                  <span class="coordinatesGrid"><span class="coordinateX">(25</span>|<span class="coordinateY">−197)</span></span>
                </div>
              </div>
            </div>
            """;

        var signal = Assert.Single(IncomingAttackDomParser.ParseDorf1Signals(
            html,
            "BRE",
            "dorf1.php?newdid=32735",
            25,
            -197,
            new DateTimeOffset(2026, 8, 22, 15, 0, 0, TimeSpan.Zero)));

        Assert.Equal("BRE", signal.VillageName);
        Assert.Equal(32735, signal.VillageId);
        Assert.Equal(25, signal.CoordX);
        Assert.Equal(-197, signal.CoordY);
    }

    [Fact]
    public void ParseIncomingAttacks_ReadsRaidIdentitySourceAndArrivalAcrossMidnight()
    {
        const string html = """
            <table class="troop_details inRaid">
              <thead><tr>
                <td class="role"><a href="/karte.php?d=74907">005</a></td>
                <td class="troopHeadline"><a class="markAttack"><img id="markSymbol_27254877"></a><a href="/karte.php?d=74508">Count Duckula raids A4</a></td>
              </tr></thead>
              <tbody>
                <tr><th class="coords"><span class="coordinateX">(-12</span>|<span class="coordinateY">34)</span></th></tr>
                <tr><th>Arrival</th><td><div class="in">in <span class="timer" value="120" data-value="120">0:02:00</span></div><div class="at">at 00:01:00</div></td></tr>
              </tbody>
            </table>
            """;
        var observedAt = new DateTimeOffset(2026, 8, 22, 23, 59, 0, TimeSpan.Zero);

        var attacks = IncomingAttackDomParser.ParseIncomingAttacks(
            html, "A4", "xy:122|15", 122, 15, observedAt);

        var attack = Assert.Single(attacks);
        Assert.Equal("27254877", attack.Id);
        Assert.Equal(IncomingAttackMovementType.Raid, attack.MovementType);
        Assert.Equal("Count Duckula", attack.SourcePlayerName);
        Assert.Equal("005", attack.SourceVillageName);
        Assert.Equal(-12, attack.SourceCoordX);
        Assert.Equal(34, attack.SourceCoordY);
        Assert.Equal(new DateTimeOffset(2026, 8, 23, 0, 1, 0, TimeSpan.Zero), attack.ArrivalAtUtc);
    }

    [Fact]
    public void ParseIncomingAttacks_UsesStableFallbackWhenMovementIdIsMissing()
    {
        const string html = """
            <table class="troop_details inAttack"><tbody><tr><td><span class="timer" data-value="60">0:01:00</span></td></tr></tbody></table>
            """;
        var observedAt = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

        var first = Assert.Single(IncomingAttackDomParser.ParseIncomingAttacks(html, "A4", "xy:1|2", 1, 2, observedAt));
        var second = Assert.Single(IncomingAttackDomParser.ParseIncomingAttacks(html, "A4", "xy:1|2", 1, 2, observedAt));

        Assert.StartsWith("fallback:", first.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(IncomingAttackMovementType.Attack, first.MovementType);
    }

    [Fact]
    public void HasOnlyIncomingFilterActive_RejectsMultipleActiveFilters()
    {
        const string correct = """
            <button class="iconFilter iconFilterActive"><img class="filterCategory filterCategory1"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory1"></button>
            <button class="iconFilter"><img class="filterCategory subFilterCategory2"></button>
            <button class="iconFilter"><img class="filterCategory subFilterCategory3"></button>
            """;
        const string incorrect = """
            <button class="iconFilter iconFilterActive"><img class="filterCategory filterCategory1"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory1"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory2"></button>
            """;

        Assert.True(IncomingAttackDomParser.HasOnlyIncomingFilterActive(correct));
        Assert.False(IncomingAttackDomParser.HasOnlyIncomingFilterActive(incorrect));
    }

    [Fact]
    public void HasOnlyIncomingFilterActive_AcceptsActiveIncomingCategoryAndOnlyIncomingSubfilter()
    {
        const string html = """
            <button type="button" class="iconFilter iconFilterActive"><img class="filterCategory filterCategory1"></button>
            <div class="filterContainer">
              <button type="button" class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory1"></button>
              <button type="button" class="iconFilter"><img class="filterCategory subFilterCategory2"></button>
              <button type="button" class="iconFilter"><img class="filterCategory subFilterCategory3"></button>
            </div>
            """;

        Assert.True(IncomingAttackDomParser.HasOnlyIncomingFilterActive(html));
    }

    [Fact]
    public void GetRequiredFilterAction_EnablesIncomingBeforeDisablingOtherSubfilters()
    {
        const string initial = """
            <button class="iconFilter iconFilterActive"><img class="filterCategory filterCategory1"></button>
            <button class="iconFilter"><img class="filterCategory subFilterCategory1"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory2"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory3"></button>
            """;
        const string incomingEnabled = """
            <button class="iconFilter iconFilterActive"><img class="filterCategory filterCategory1"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory1"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory2"></button>
            <button class="iconFilter iconFilterActive"><img class="filterCategory subFilterCategory3"></button>
            """;

        Assert.Equal(IncomingAttackFilterAction.EnableIncomingSubfilter, IncomingAttackDomParser.GetRequiredFilterAction(initial));
        Assert.Equal(IncomingAttackFilterAction.DisableReinforcementsSubfilter, IncomingAttackDomParser.GetRequiredFilterAction(incomingEnabled));
    }
}

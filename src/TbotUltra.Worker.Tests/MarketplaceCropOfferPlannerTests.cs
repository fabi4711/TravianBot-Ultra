using TbotUltra.Core.Travian;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class MarketplaceCropOfferPlannerTests
{
    private static readonly MarketplaceCropOfferSettings Settings = new(80, 50, 50, 3);

    [Fact]
    public void Plan_OffersFullMerchantLoadsForTheScarcestResource()
    {
        var plan = Plan(Stock(wood: 30000, clay: 4000, iron: 5000, crop: 50000), []);

        Assert.Equal(
            [new MarketplaceCropOffer(10000, "clay", 5000), new MarketplaceCropOffer(10000, "iron", 5000)],
            plan.Offers);
    }

    [Fact]
    public void Plan_SkipsBelowTheThreshold()
    {
        var plan = Plan(Stock(crop: 43000), []);

        Assert.Empty(plan.Offers);
        Assert.Contains("below the threshold", plan.Reason);
    }

    [Fact]
    public void Plan_CountsOpenOffersAgainstTheLimitAndTheIncomingResources()
    {
        var open = new[] { new MarketplaceOpenOffer("clay", 5000, "crop", 10000) };

        var plan = Plan(Stock(wood: 30000, clay: 4000, iron: 5000, crop: 55000), open);

        Assert.Equal(
            [new MarketplaceCropOffer(10000, "iron", 5000), new MarketplaceCropOffer(10000, "clay", 5000)],
            plan.Offers);
    }

    [Fact]
    public void Plan_StopsAtTheOpenOfferLimit()
    {
        var open = Enumerable.Range(0, 3).Select(_ => new MarketplaceOpenOffer("clay", 5000, "crop", 10000)).ToList();

        var plan = Plan(Stock(crop: 55000), open);

        Assert.Empty(plan.Offers);
        Assert.Contains("limit", plan.Reason);
    }

    [Fact]
    public void Plan_NeverAsksForMoreThanTheWarehouseCanTake()
    {
        var plan = Plan(Stock(wood: 50000, clay: 50000, iron: 50000, crop: 55000), []);

        Assert.Empty(plan.Offers);
        Assert.Contains("no offer fits", plan.Reason);
    }

    [Fact]
    public void Plan_UsesTheRatioAndTheFreeMerchants()
    {
        var plan = MarketplaceCropOfferPlanner.Plan(
            Stock(crop: 55000), 55000, 55000, [], 1, 10000, Settings with { RatioPercent = 100 });

        Assert.Equal([new MarketplaceCropOffer(10000, "wood", 10000)], plan.Offers);
    }

    private static MarketplaceCropOfferPlan Plan(IReadOnlyDictionary<string, long> stock, IReadOnlyList<MarketplaceOpenOffer> open)
        => MarketplaceCropOfferPlanner.Plan(stock, 55000, 55000, open, 5, 10000, Settings);

    private static IReadOnlyDictionary<string, long> Stock(long wood = 0, long clay = 0, long iron = 0, long crop = 0)
        => new Dictionary<string, long> { ["wood"] = wood, ["clay"] = clay, ["iron"] = iron, ["crop"] = crop };

    [Theory]
    [InlineData("textButtonV2 toggle activeNotClickable active orange", true)]
    [InlineData("textButtonV2 toggle activeNotClickable grey", false)]
    [InlineData(null, false)]
    public void HasCssClass_MatchesWholeClassNamesOnly(string? classes, bool expected)
    {
        Assert.Equal(expected, TbotUltra.Worker.Services.TravianClient.HasCssClass(classes, "active"));
    }
}

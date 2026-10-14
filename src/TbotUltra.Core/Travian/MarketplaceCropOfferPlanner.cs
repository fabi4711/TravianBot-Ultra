namespace TbotUltra.Core.Travian;

public sealed record MarketplaceOpenOffer(string WantedResource, long WantedAmount, string OfferedResource, long OfferedAmount);

public sealed record MarketplaceCropOffer(long OfferedCrop, string WantedResource, long WantedAmount);

public sealed record MarketplaceCropOfferPlan(IReadOnlyList<MarketplaceCropOffer> Offers, string Reason);

public sealed record MarketplaceCropOfferSettings(int ThresholdPercent, int KeepPercent, int RatioPercent, int MaxOpenOffers);

/// <summary>
/// Plans marketplace offers that trade surplus crop for the scarcest warehouse resource. Each offer is one full
/// merchant load, and the wanted amount must still fit into the warehouse once every open offer is accepted.
/// </summary>
public static class MarketplaceCropOfferPlanner
{
    private const int WarehouseFillLimitPercent = 95;
    private static readonly string[] WantedResources = ["wood", "clay", "iron"];

    public static MarketplaceCropOfferPlan Plan(
        IReadOnlyDictionary<string, long> stock,
        long warehouseCapacity,
        long granaryCapacity,
        IReadOnlyList<MarketplaceOpenOffer> openOffers,
        int freeMerchants,
        long merchantCapacity,
        MarketplaceCropOfferSettings settings)
    {
        if (warehouseCapacity <= 0 || granaryCapacity <= 0)
        {
            return Skip("storage capacity is unknown");
        }

        var crop = stock.GetValueOrDefault("crop");
        var threshold = granaryCapacity * settings.ThresholdPercent / 100;
        if (crop < threshold)
        {
            return Skip($"crop {crop} is below the threshold {threshold} ({settings.ThresholdPercent}%)");
        }

        var slots = settings.MaxOpenOffers - openOffers.Count;
        if (slots <= 0)
        {
            return Skip($"{openOffers.Count} open offer(s) reached the limit of {settings.MaxOpenOffers}");
        }

        if (freeMerchants <= 0 || merchantCapacity <= 0)
        {
            return Skip("no free merchant");
        }

        var surplus = crop - granaryCapacity * settings.KeepPercent / 100;
        var incoming = WantedResources.ToDictionary(
            key => key,
            key => openOffers.Where(offer => offer.WantedResource == key).Sum(offer => offer.WantedAmount));
        var offers = new List<MarketplaceCropOffer>();
        var warehouseLimit = warehouseCapacity * WarehouseFillLimitPercent / 100;
        while (offers.Count < Math.Min(slots, freeMerchants))
        {
            var offered = merchantCapacity;
            if (surplus < offered)
            {
                break;
            }

            var wanted = (long)Math.Ceiling(offered * settings.RatioPercent / 100d);
            var resource = WantedResources
                .Where(key => stock.GetValueOrDefault(key) + incoming[key] + wanted <= warehouseLimit)
                .OrderBy(key => stock.GetValueOrDefault(key) + incoming[key])
                .FirstOrDefault();
            if (resource is null)
            {
                break;
            }

            offers.Add(new MarketplaceCropOffer(offered, resource, wanted));
            incoming[resource] += wanted;
            surplus -= offered;
        }

        return offers.Count == 0
            ? Skip($"no offer fits (surplus {Math.Max(0, surplus)}, warehouse {warehouseCapacity})")
            : new MarketplaceCropOfferPlan(offers, $"{offers.Count} offer(s) planned from crop {crop}");
    }

    private static MarketplaceCropOfferPlan Skip(string reason) => new([], reason);
}

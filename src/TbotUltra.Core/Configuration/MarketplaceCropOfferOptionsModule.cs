using Microsoft.Extensions.Configuration;

namespace TbotUltra.Core.Configuration;

internal sealed record MarketplaceCropOfferOptions(
    bool Enabled,
    List<string> VillageNames,
    int ThresholdPercent,
    int KeepPercent,
    int RatioPercent,
    int MaxOpenOffers);

internal static class MarketplaceCropOfferOptionsModule
{
    internal static IReadOnlyList<string> AccountScopedKeys { get; } =
    [
        BotOptionPayloadKeys.MarketplaceCropOfferEnabled,
        BotOptionPayloadKeys.MarketplaceCropOfferVillageNames,
        BotOptionPayloadKeys.MarketplaceCropOfferThresholdPercent,
        BotOptionPayloadKeys.MarketplaceCropOfferKeepPercent,
        BotOptionPayloadKeys.MarketplaceCropOfferRatioPercent,
        BotOptionPayloadKeys.MarketplaceCropOfferMaxOpenOffers,
    ];

    // Travian accepts offers between 1:2 and 2:1; asking for more than offered makes no sense for a surplus.
    internal const int MinRatioPercent = 50;
    internal const int MaxRatioPercent = 100;

    internal static MarketplaceCropOfferOptions FromConfiguration(IConfiguration configuration)
        => new(
            configuration.GetValue(BotOptionPayloadKeys.MarketplaceCropOfferEnabled, false),
            VillageNameListPayloadCodec.Normalize(
                configuration.GetSection(BotOptionPayloadKeys.MarketplaceCropOfferVillageNames).Get<List<string>>() ?? []),
            Math.Clamp(configuration.GetValue(BotOptionPayloadKeys.MarketplaceCropOfferThresholdPercent, 80), 0, 100),
            Math.Clamp(configuration.GetValue(BotOptionPayloadKeys.MarketplaceCropOfferKeepPercent, 50), 0, 99),
            Math.Clamp(configuration.GetValue(BotOptionPayloadKeys.MarketplaceCropOfferRatioPercent, 50), MinRatioPercent, MaxRatioPercent),
            Math.Clamp(configuration.GetValue(BotOptionPayloadKeys.MarketplaceCropOfferMaxOpenOffers, 3), 1, 20));

    internal static BotOptions ApplyTo(this MarketplaceCropOfferOptions values, BotOptions source)
        => source with
        {
            MarketplaceCropOfferEnabled = values.Enabled,
            MarketplaceCropOfferVillageNames = values.VillageNames,
            MarketplaceCropOfferThresholdPercent = values.ThresholdPercent,
            MarketplaceCropOfferKeepPercent = values.KeepPercent,
            MarketplaceCropOfferRatioPercent = values.RatioPercent,
            MarketplaceCropOfferMaxOpenOffers = values.MaxOpenOffers,
        };
}

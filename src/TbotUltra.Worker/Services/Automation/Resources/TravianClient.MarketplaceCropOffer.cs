using Microsoft.Playwright;
using System.Text.Json;
using System.Text.Json.Serialization;
using TbotUltra.Core.Travian;

namespace TbotUltra.Worker.Services;

public sealed partial class TravianClient
{
    private const int MarketplaceSellTab = 2;
    // Short on purpose: a busy crop village fills its granary from the threshold in well under half an hour.
    private const int MarketplaceCropOfferIdleSeconds = 600;
    private const string CreateOfferFormSelector = "#createOfferForm";

    // Travian icon classes per resource; the offer dialog uses "lumber" for wood.
    private static readonly IReadOnlyDictionary<string, string> MarketplaceResourceClasses =
        new Dictionary<string, string> { ["wood"] = "lumber", ["clay"] = "clay", ["iron"] = "iron", ["crop"] = "crop" };

    public async Task<string> CreateMarketplaceCropOffersAsync(CancellationToken cancellationToken = default)
    {
        var villageNames = _config.MarketplaceCropOfferVillageNames ?? [];
        Notify($"[crop-offer] starting — villages='{string.Join(", ", villageNames)}' threshold={_config.MarketplaceCropOfferThresholdPercent}% keep={_config.MarketplaceCropOfferKeepPercent}% ratio={_config.MarketplaceCropOfferRatioPercent}% max_open={_config.MarketplaceCropOfferMaxOpenOffers}");
        if (villageNames.Count == 0)
        {
            return $"Crop offers: no village selected. queue_wait_seconds={MarketplaceCropOfferIdleSeconds}";
        }

        await EnsureLoggedInAsync(cancellationToken: cancellationToken);
        var settings = new MarketplaceCropOfferSettings(
            _config.MarketplaceCropOfferThresholdPercent,
            _config.MarketplaceCropOfferKeepPercent,
            _config.MarketplaceCropOfferRatioPercent,
            _config.MarketplaceCropOfferMaxOpenOffers);
        var villages = await ReadVillagesAsync(cancellationToken);
        var created = 0;
        foreach (var villageName in villageNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var village = villages.FirstOrDefault(v => string.Equals(v.Name, villageName, StringComparison.OrdinalIgnoreCase));
            if (village is null)
            {
                Notify($"[crop-offer] village '{villageName}' was not found; skipping it.");
                continue;
            }

            created += await CreateMarketplaceCropOffersInVillageAsync(village, settings, cancellationToken);
        }

        return created > 0
            ? $"Crop offers: created {created} offer(s)."
            : $"Crop offers: nothing to offer. queue_wait_seconds={MarketplaceCropOfferIdleSeconds}";
    }

    private async Task<int> CreateMarketplaceCropOffersInVillageAsync(
        TbotUltra.Worker.Domain.Village village,
        MarketplaceCropOfferSettings settings,
        CancellationToken cancellationToken)
    {
        var status = await ReadResourceTransferVillageStatusAsync(village, cancellationToken);
        var stock = TryBuildResourceTransferStock(status);
        if (stock is null)
        {
            Notify($"[crop-offer] skip '{village.Name}': storage data is incomplete.");
            return 0;
        }

        var marketplace = FindMarketplaceBuilding(status.Buildings);
        if (marketplace?.SlotId is not > 0)
        {
            Notify($"[crop-offer] skip '{village.Name}': Marketplace was not found.");
            return 0;
        }

        await OpenSlotTabAsync(marketplace.SlotId.Value, MarketplaceSellTab, cancellationToken);
        var state = await ReadMarketplaceSellStateAsync();
        var openOffers = state.Offers.Select(ToOpenOffer).ToList();
        var plan = MarketplaceCropOfferPlanner.Plan(
            stock.Resources,
            stock.WarehouseCapacity,
            stock.GranaryCapacity,
            openOffers,
            state.FreeMerchants ?? 0,
            state.MerchantCapacity ?? 0,
            settings);
        Notify(
            $"[crop-offer] '{village.Name}' crop={stock.Resources.GetValueOrDefault("crop")}/{stock.GranaryCapacity} "
            + $"merchants={state.FreeMerchants?.ToString() ?? "unknown"} capacity={state.MerchantCapacity?.ToString() ?? "unknown"} "
            + $"open_offers={openOffers.Count} plan: {plan.Reason}.");

        var created = 0;
        foreach (var offer in plan.Offers)
        {
            if (!await TryCreateMarketplaceCropOfferAsync(village.Name, offer, openOffers.Count + created, cancellationToken))
            {
                break;
            }

            created++;
            Notify($"[crop-offer] '{village.Name}' offered crop={offer.OfferedCrop} for {offer.WantedResource}={offer.WantedAmount}.");
            await ApplyActionDelayAsync(cancellationToken);
        }

        return created;
    }

    private async Task<bool> TryCreateMarketplaceCropOfferAsync(
        string villageName,
        MarketplaceCropOffer offer,
        int offersBefore,
        CancellationToken cancellationToken)
    {
        if (!await TryClickFirstVisibleEnabledAsync("#build button.textButtonV2.green.withText", cancellationToken, reason: "open create offer dialog"))
        {
            Notify($"[crop-offer] '{villageName}': create offer button not found url='{_page.Url}'.");
            return false;
        }

        var form = _page.Locator(CreateOfferFormSelector);
        try
        {
            await form.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = _config.TimeoutMs });
            var pickers = form.Locator(".resourcePicker");
            if (!await FillOfferPickerAsync(pickers.Nth(0), "crop", offer.OfferedCrop, cancellationToken)
                || !await FillOfferPickerAsync(pickers.Nth(1), offer.WantedResource, offer.WantedAmount, cancellationToken))
            {
                Notify($"[crop-offer] '{villageName}': the dialog did not keep crop={offer.OfferedCrop} {offer.WantedResource}={offer.WantedAmount}; cancelled.");
                return false;
            }

            var submit = _page.Locator(".createOfferDialog button.create").First;
            if (HasCssClass(await submit.GetAttributeAsync("class"), "disabled"))
            {
                Notify($"[crop-offer] '{villageName}': create button is disabled; cancelled.");
                return false;
            }

            await DelayBeforeClickAsync(cancellationToken);
            await ClickLocatorAsync(submit, "create-offer", cancellationToken);
            for (var attempt = 0; attempt < 10; attempt++)
            {
                await Task.Delay(500, cancellationToken);
                if ((await ReadMarketplaceSellStateAsync()).Offers.Count > offersBefore)
                {
                    return true;
                }
            }

            Notify($"[crop-offer] '{villageName}': the new offer did not show up in the offer list url='{_page.Url}'.");
            return false;
        }
        finally
        {
            await CloseCreateOfferDialogAsync(cancellationToken);
        }
    }

    // The game silently lowers amounts it cannot carry or stock, so the typed values are read back.
    private async Task<bool> FillOfferPickerAsync(ILocator picker, string resource, long amount, CancellationToken cancellationToken)
    {
        var resourceClass = MarketplaceResourceClasses[resource];
        var toggle = picker.Locator($".resource.{resourceClass} button.selectResource").First;
        if (!HasCssClass(await toggle.GetAttributeAsync("class"), "active"))
        {
            await DelayBeforeClickAsync(cancellationToken);
            await ClickLocatorAsync(toggle, $"select-offer-{resourceClass}", cancellationToken);
        }

        if (!HasCssClass(await toggle.GetAttributeAsync("class"), "active"))
        {
            Notify($"[crop-offer] resource toggle '{resourceClass}' did not become active.");
            return false;
        }

        var input = picker.Locator(".input.amount input").First;
        await DelayBeforeClickAsync(cancellationToken, "focus offer amount");
        await TypeHumanlyAsync(input, amount.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        await Task.Delay(300, cancellationToken);
        var typed = ExpansionCalculator.ParseWholeNumber(await input.InputValueAsync());
        if (typed != amount)
        {
            Notify($"[crop-offer] '{resourceClass}' amount reads {typed?.ToString() ?? "empty"} instead of {amount}.");
            return false;
        }

        return true;
    }

    // Every resource toggle carries "activeNotClickable", so a substring check for "active" always matches.
    internal static bool HasCssClass(string? classes, string name)
        => (classes ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(name, StringComparer.Ordinal);

    private async Task CloseCreateOfferDialogAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cancel = _page.Locator(".createOfferDialog button.cancel").First;
            if (await cancel.CountAsync() > 0 && await cancel.IsVisibleAsync())
            {
                await DelayBeforeClickAsync(cancellationToken, "close offer dialog");
                await ClickLocatorAsync(cancel, "close-create-offer-dialog", cancellationToken);
            }
        }
        catch (PlaywrightException ex)
        {
            Notify($"[crop-offer] closing the offer dialog failed: {ex.Message}");
        }
    }

    // Playwright returned null for the nested offer list once rows existed, so the script hands back JSON text.
    private async Task<MarketplaceSellStateJs> ReadMarketplaceSellStateAsync()
        => ParseMarketplaceSellState(await _page.EvaluateAsync<string>(MarketplaceSellStateScript));

    internal static MarketplaceSellStateJs ParseMarketplaceSellState(string? json)
    {
        var state = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<MarketplaceSellStateJs>(json);
        return state is null ? new MarketplaceSellStateJs() : state with { Offers = state.Offers ?? [] };
    }

    private static MarketplaceOpenOffer ToOpenOffer(MarketplaceSellOfferJs offer)
    {
        var wanted = MarketplaceResourceClasses.FirstOrDefault(pair => pair.Value == offer.WantedResource).Key ?? offer.WantedResource ?? string.Empty;
        var offered = MarketplaceResourceClasses.FirstOrDefault(pair => pair.Value == offer.OfferedResource).Key ?? offer.OfferedResource ?? string.Empty;
        return new MarketplaceOpenOffer(wanted, offer.WantedAmount ?? 0, offered, offer.OfferedAmount ?? 0);
    }

    private const string MarketplaceSellStateScript =
        """
            () => {
              const digits = (value) => {
                const text = String(value || '').replace(/[^\d]/g, '');
                return text ? Number.parseInt(text, 10) : null;
              };
              const resourceOf = (cell) => {
                const icon = cell?.querySelector('i[class$="_small"]');
                return icon ? icon.className.replace(/_small.*$/, '') : null;
              };
              const available = String(document.querySelector('.merchantsInformation .available .value')?.textContent || '').split('/');
              const offers = [...document.querySelectorAll('#build table.offers tbody tr')]
                .filter((row) => row.querySelector('td.wanted i') && row.querySelector('td.offered i'))
                .map((row) => ({
                  wantedResource: resourceOf(row.querySelector('td.wanted')),
                  wantedAmount: digits(row.querySelector('td.wanted span')?.textContent),
                  offeredResource: resourceOf(row.querySelector('td.offered')),
                  offeredAmount: digits(row.querySelector('td.offered span')?.textContent),
                }));
              return JSON.stringify({
                freeMerchants: available.length === 2 ? digits(available[0]) : null,
                merchantCapacity: digits(document.querySelector('.merchantsInformation .capacity .value')?.textContent),
                offers,
              });
            }
            """;

    internal sealed record MarketplaceSellStateJs
    {
        [JsonPropertyName("freeMerchants")]
        public int? FreeMerchants { get; init; }

        [JsonPropertyName("merchantCapacity")]
        public long? MerchantCapacity { get; init; }

        [JsonPropertyName("offers")]
        public List<MarketplaceSellOfferJs> Offers { get; init; } = [];
    }

    internal sealed record MarketplaceSellOfferJs
    {
        [JsonPropertyName("wantedResource")]
        public string? WantedResource { get; init; }

        [JsonPropertyName("wantedAmount")]
        public long? WantedAmount { get; init; }

        [JsonPropertyName("offeredResource")]
        public string? OfferedResource { get; init; }

        [JsonPropertyName("offeredAmount")]
        public long? OfferedAmount { get; init; }
    }
}

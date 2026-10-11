using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using TbotUltra.Core.Travian;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

public sealed record ExpansionStepResult(bool Completed, int? WaitSeconds, string Message);

public sealed partial class TravianClient
{
    private const string SettlerTroopType = "Settler";
    private const int SettlerSlot = 10;

    private static readonly (ExpansionBuildingType Type, string Name)[] ExpansionBuildings =
    [
        (ExpansionBuildingType.Palace, "Palace"),
        (ExpansionBuildingType.Residence, "Residence"),
        (ExpansionBuildingType.CommandCenter, "Command Center"),
    ];

    /// <summary>
    /// Trains the settlers still needed for the free expansion slots of the active village's
    /// Residence/Palace/Command Center. Completes when the slots are covered.
    /// </summary>
    public async Task<ExpansionStepResult> TrainSettlersAsync(CancellationToken cancellationToken = default)
    {
        LogFunctionStarted();
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);
        var status = await ReadBuildingsStatusAsync(cancellationToken);
        var expansion = FindExpansionBuilding(status.Buildings);
        if (expansion is null)
        {
            return new ExpansionStepResult(false, 3600, "Train settlers: no Residence, Palace or Command Center in this village yet.");
        }

        var (building, type) = expansion.Value;
        var level = building.Level ?? 0;
        var usedSlots = Math.Max(0, status.Villages.Count - 1);
        var settlersAtHome = await ReadSettlersAtHomeAsync(cancellationToken);

        // The Residence/Palace opens on its management tab; settlers are trained on tab s=1.
        await EnsurePageForReadAsync($"{Paths.BuildBySlot(building.SlotId!.Value)}&s=1", "train settlers", cancellationToken);
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);
        var queue = await ReadTroopTrainingQueueFromCurrentPageAsync(cancellationToken);
        var settlersInTraining = CountSettlersInQueue(queue);

        // Used slots are approximated by the number of own villages beyond the first; exact per-village
        // expansion data lives on the Residence expansion tab and is not read yet.
        var plan = ExpansionCalculator.PlanSettlerTraining(type, level, usedSlots, settlersAtHome, settlersInTraining, 0);
        Notify($"[expansion] {type} level {level}: slots={plan.ExpansionSlots}, free={plan.FreeSlots}, home={settlersAtHome}, training={settlersInTraining}, toTrain={plan.SettlersToTrain}. {plan.Reason}");
        if (plan.ExpansionSlots == 0)
        {
            return new ExpansionStepResult(false, 3600, $"Train settlers: {plan.Reason}");
        }

        if (plan.SettlersToTrain == 0)
        {
            return new ExpansionStepResult(true, null, $"Train settlers: done. {plan.Reason}");
        }

        var unitId = TroopCatalog.ResolveTravianUnitId(status.Tribe, SettlerTroopType);
        var inputName = await ResolveTroopTrainingInputNameAsync(unitId ?? 0, SettlerSlot);
        if (string.IsNullOrWhiteSpace(inputName))
        {
            Notify($"[expansion] settler input not found on '{_page.Url}'.");
            return new ExpansionStepResult(false, 1800, "Train settlers: settler training input not found on the building page.");
        }

        var buildInfo = await ReadTroopUnitBuildInfoFromCurrentPageAsync(inputName, cancellationToken);
        if (!buildInfo.Found || !buildInfo.CanTrain)
        {
            return new ExpansionStepResult(false, 1200, "Train settlers: settlers cannot be trained right now (resources or requirements).");
        }

        var affordable = MaxAffordable(status.Resources, buildInfo);
        var amount = Math.Min(plan.SettlersToTrain, affordable);
        if (amount <= 0)
        {
            return new ExpansionStepResult(false, 1200, $"Train settlers: not enough resources for one settler (needs {buildInfo.WoodCost}/{buildInfo.ClayCost}/{buildInfo.IronCost}/{buildInfo.CropCost}).");
        }

        await DelayBeforeClickAsync(cancellationToken, "train settlers");
        if (!await SubmitTroopTrainingFromCurrentPageAsync(inputName, amount, useMaxShortcut: false, cancellationToken))
        {
            return new ExpansionStepResult(false, 900, $"Train settlers: submitting {amount} settler(s) failed.");
        }

        Notify($"[expansion] queued {amount} settler(s) at {type}.");
        var remaining = plan.SettlersToTrain - amount;
        var trainingSeconds = (buildInfo.TrainingSeconds ?? 600) * amount;
        return remaining > 0
            ? new ExpansionStepResult(false, Math.Clamp(trainingSeconds, 600, 3600), $"Train settlers: queued {amount}, {remaining} still needed.")
            : new ExpansionStepResult(true, null, $"Train settlers: queued {amount}; all free slots are covered.");
    }

    /// <summary>
    /// Sends 3 settlers from the active village to found a new village on the given free field.
    /// </summary>
    public async Task<ExpansionStepResult> FoundVillageAsync(int x, int y, CancellationToken cancellationToken = default)
    {
        LogFunctionStarted();
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);
        var settlersAtHome = await ReadSettlersAtHomeAsync(cancellationToken);
        if (settlersAtHome < ExpansionCalculator.SettlersPerVillage)
        {
            return new ExpansionStepResult(false, 1800, $"Found village ({x}|{y}): only {settlersAtHome}/{ExpansionCalculator.SettlersPerVillage} settlers at home.");
        }

        var detailsPath = $"/position_details.php?x={x.ToString(CultureInfo.InvariantCulture)}&y={y.ToString(CultureInfo.InvariantCulture)}";
        await GotoAsync(detailsPath, cancellationToken);
        await EnsureLoggedInAsync(cancellationToken: cancellationToken);

        var foundLink = await FindFoundVillageLinkAsync();
        if (foundLink is null)
        {
            await LogPageLinksForDiagnosisAsync("found village link");
            return new ExpansionStepResult(false, null, $"Found village ({x}|{y}): no 'Found new village' action on the field page (occupied, oasis, or not enough culture points?).");
        }

        await DelayBeforeClickAsync(cancellationToken, "found new village");
        await ClickLocatorAsync(foundLink, "found-new-village", cancellationToken);
        await WaitForPageReadyAsync(cancellationToken);
        Notify($"[expansion] after 'Found new village' url='{_page.Url}'.");

        if (!await HasConfirmButtonAsync())
        {
            // Some worlds show the settler send form first; fill 3 settlers if needed and submit it.
            var settlerCount = await ReadAvailableTroopCountAsync($"t{SettlerSlot}", cancellationToken);
            if (settlerCount is > 0)
            {
                await TryFillTroopInputAsync($"t{SettlerSlot}", SettlerTroopType, ExpansionCalculator.SettlersPerVillage, cancellationToken);
            }

            await DelayBeforeClickAsync(cancellationToken, "found village: send");
            if (!await TryClickConfirmButtonAsync(cancellationToken) || !await WaitForSendTroopsConfirmationPageAsync(cancellationToken))
            {
                var error = await ReadReinforcementFormErrorAsync(cancellationToken);
                await LogPageLinksForDiagnosisAsync("found village form");
                return new ExpansionStepResult(false, null, $"Found village ({x}|{y}): send form not accepted{(error is null ? "." : $": {error}")}");
            }
        }

        var confirm = _page.Locator("button#confirmSendTroops, button[name='confirmSendTroops'], button.rallyPointConfirm").First;
        if (await confirm.CountAsync() == 0)
        {
            await LogPageLinksForDiagnosisAsync("found village confirm");
            return new ExpansionStepResult(false, null, $"Found village ({x}|{y}): confirm button not found.");
        }

        await DelayBeforeClickAsync(cancellationToken, "found village: confirm");
        await ClickLocatorAsync(confirm, "found-village-confirm", cancellationToken);
        await WaitForSendTroopsCompletionAsync(cancellationToken);
        var confirmError = await ReadReinforcementFormErrorAsync(cancellationToken);
        if (confirmError is not null)
        {
            return new ExpansionStepResult(false, null, $"Found village ({x}|{y}): {confirmError}");
        }

        Notify($"[expansion] settlers sent to found ({x}|{y}).");
        return new ExpansionStepResult(true, null, $"Found village ({x}|{y}): settlers sent.");
    }

    private static (Building Building, ExpansionBuildingType Type)? FindExpansionBuilding(IReadOnlyList<Building> buildings)
    {
        foreach (var (type, name) in ExpansionBuildings)
        {
            var match = buildings.FirstOrDefault(building => building.SlotId is > 0
                && (building.Gid == (int)type || string.Equals(building.Name, name, StringComparison.OrdinalIgnoreCase)));
            if (match is not null)
            {
                return (match, type);
            }
        }

        return null;
    }

    private async Task<int> ReadSettlersAtHomeAsync(CancellationToken cancellationToken)
    {
        await EnsureRallyPointAndOpenSendTroopsPageAsync(cancellationToken, allowReuseCurrentPage: true);
        var count = await ReadAvailableTroopCountAsync($"t{SettlerSlot}", cancellationToken);
        Notify($"[expansion] settlers at home: {count?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}.");
        return (int)Math.Max(0, count ?? 0);
    }

    internal static int CountSettlersInQueue(IReadOnlyList<BuildQueueItem> queue)
    {
        var total = 0;
        foreach (var item in queue)
        {
            var match = Regex.Match(item.Text ?? string.Empty, @"(\d+)\s*(?:Settlers?|Siedler)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                total += int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            }
        }

        return total;
    }

    private static int MaxAffordable(IReadOnlyDictionary<string, string> resources, TroopUnitBuildInfo info)
    {
        long Read(string key) => resources.TryGetValue(key, out var raw)
            && long.TryParse(Regex.Replace(raw ?? string.Empty, @"[^\d]", string.Empty), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : long.MaxValue;
        static long Per(long stock, int cost) => cost > 0 ? stock / cost : long.MaxValue;

        var max = new[]
        {
            Per(Read("wood"), info.WoodCost),
            Per(Read("clay"), info.ClayCost),
            Per(Read("iron"), info.IronCost),
            Per(Read("crop"), info.CropCost),
        }.Min();
        return (int)Math.Min(max, int.MaxValue);
    }

    private async Task<ILocator?> FindFoundVillageLinkAsync()
    {
        var candidates = new[]
        {
            "a[href*='eventType=10']",
            "a:has-text('Found new village')",
            "button:has-text('Found new village')",
            "a:has-text('found new village')",
        };
        foreach (var selector in candidates)
        {
            var locator = _page.Locator(selector).First;
            if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
            {
                Notify($"[expansion] found-village action matched '{selector}'.");
                return locator;
            }
        }

        return null;
    }

    private async Task<bool> HasConfirmButtonAsync() =>
        await _page.Locator("button#confirmSendTroops, button[name='confirmSendTroops'], button.rallyPointConfirm").CountAsync() > 0;

    private async Task LogPageLinksForDiagnosisAsync(string stage)
    {
        try
        {
            var summary = await _page.EvaluateAsync<string>(
                """
                () => [...document.querySelectorAll('#content a, #content button')]
                  .map(e => `${(e.textContent || '').replace(/\s+/g, ' ').trim().slice(0, 40)} -> ${e.getAttribute('href') || e.id || e.className}`)
                  .filter(t => t.length > 4).slice(0, 40).join(' | ')
                """);
            Notify($"[expansion] diagnosis ({stage}) url='{_page.Url}': {summary}");
        }
        catch (PlaywrightException ex)
        {
            Notify($"[expansion] diagnosis ({stage}) failed: {ex.Message}");
        }
    }
}

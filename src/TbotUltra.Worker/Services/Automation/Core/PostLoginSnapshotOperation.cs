using TbotUltra.Core.Accounts;
using TbotUltra.Core.Configuration;
using TbotUltra.Worker.Configuration;
using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services.Automation;

/// <summary>Owns the post-login snapshot and stable-account-signal workflow.</summary>
internal sealed class PostLoginSnapshotOperation(IPostLoginSnapshotClient client, AccountAnalysisStore accountAnalysisStore)
{
    public async Task<PostLoginSnapshot> LoadAsync(
        BotOptions options,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        log($"Loading post-login data for server {options.ServerName}.");

        accountAnalysisStore.TryLoad(client.AccountName, out var persistedAnalysis, client.ServerUrl);
        var newAccountAnalysisPending = NewAccountAnalysisDecisions.IsPending(
            options.PostLoginAnalyzeNewAccount,
            persistedAnalysis?.NewAccountAnalysisCompleted);

        HeroInventoryResources? heroInventory = null;
        if (options.PostLoginAnalyzeHeroInventory || newAccountAnalysisPending)
        {
            try
            {
                heroInventory = await client.ReadHeroInventoryResourcesAsync(cancellationToken, suppressUiSync: true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log($"[hero-inventory] post-login read failed (continuing without it): {ex.Message}");
            }
        }

        var hasPersistedVillageSnapshot = persistedAnalysis?.Villages is { Count: > 0 };
        log(hasPersistedVillageSnapshot
            ? "[post-login] reusing stable village snapshot; merging current sidebar instead of opening profile."
            : "[post-login] no stable village snapshot; opening profile for complete cold-start village data.");

        var accountSnapshot = await client.ReadAccountSnapshotAsync(
            forceRefreshVillages: !hasPersistedVillageSnapshot,
            preferCurrentPageVillages: hasPersistedVillageSnapshot,
            restorePageAfterProfile: false,
            suppressEnsureUiSync: true,
            skipOverviewNavigation: heroInventory is not null,
            cancellationToken);

        var villageStatus = await client.ReadVillageStatusAsync(
            accountSnapshot.Villages,
            [],
            cancellationToken);

        if (options.PostLoginReadTroopTrainingQueue)
        {
            var troopQueues = await client.ReadTroopTrainingQueuesAsync(villageStatus.Buildings, cancellationToken);
            villageStatus = villageStatus with { TroopTrainingQueues = troopQueues };
        }

        var inboxStatus = new InboxStatus(villageStatus.UnreadMessages, villageStatus.UnreadReports);
        var adventureCount = await client.RefreshAdventureCountAsync(forceReload: false, cancellationToken);

        PersistStableAccountSignals(
            accountSnapshot.Tribe,
            accountSnapshot.Villages,
            newAccountAnalysisPending,
            log);

        return new PostLoginSnapshot(villageStatus, inboxStatus, adventureCount, heroInventory, newAccountAnalysisPending);
    }

    private void PersistStableAccountSignals(
        string? fallbackTribe,
        IReadOnlyList<Village> villages,
        bool newAccountAnalysisPending,
        Action<string> log)
    {
        var completed = accountAnalysisStore.Update(client.AccountName, client.ServerUrl, existing =>
        {
            var tribe = IsKnownTribe(client.KnownAccountTribe)
                ? client.KnownAccountTribe!
                : IsKnownTribe(fallbackTribe)
                    ? fallbackTribe!
                    : existing?.Tribe ?? "Unknown";
            var goldClubEnabled = client.KnownGoldClubEnabled == true || existing?.GoldClubEnabled == true;
            if (!IsKnownTribe(tribe) && !goldClubEnabled && villages.Count == 0)
            {
                return null;
            }

            return new AccountAnalysisSnapshot(
                SchemaVersion: AccountAnalysisConstants.CurrentSchemaVersion,
                AnalyzedAtUtc: DateTimeOffset.UtcNow,
                AccountName: client.AccountName,
                ServerUrl: client.ServerUrl,
                Tribe: IsKnownTribe(tribe) ? tribe : "Unknown",
                GoldClubEnabled: goldClubEnabled,
                BuildingCatalog: existing?.BuildingCatalog ?? (IsKnownTribe(tribe) ? BuildingCatalogService.GetCatalogForTribe(tribe) : []),
                AutoCelebrationEnabled: existing?.AutoCelebrationEnabled,
                AutomationLoopEnabledGroups: existing?.AutomationLoopEnabledGroups,
                AutomationLoopVisibleGroups: existing?.AutomationLoopVisibleGroups,
                WorldUid: existing?.WorldUid,
                Villages: villages.Count > 0 ? villages.Select(village => village with { }).ToList() : existing?.Villages,
                NewAccountAnalysisCompleted: existing is null
                    ? (newAccountAnalysisPending ? false : null)
                    : existing.NewAccountAnalysisCompleted);
        });
        if (completed is null)
        {
            return;
        }

        log($"[cache] stable account signals saved for '{completed.AccountName}' (tribe={completed.Tribe}, goldclub={completed.GoldClubEnabled}).");
        log($"[goldclub] active={completed.GoldClubEnabled}");
    }

    private static bool IsKnownTribe(string? tribe)
        => !string.IsNullOrWhiteSpace(tribe)
           && !string.Equals(tribe, "Unknown", StringComparison.OrdinalIgnoreCase);
}

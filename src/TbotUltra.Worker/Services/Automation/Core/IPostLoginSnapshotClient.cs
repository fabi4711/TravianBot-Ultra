using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

/// <summary>Reads the bounded set of post-login observations needed to build the Desktop snapshot.</summary>
internal interface IPostLoginSnapshotClient
{
    string AccountName { get; }
    string ServerUrl { get; }
    string? KnownAccountTribe { get; }
    bool? KnownGoldClubEnabled { get; }
    Task<HeroInventoryResources> ReadHeroInventoryResourcesAsync(CancellationToken cancellationToken = default, bool suppressUiSync = false);
    Task<AccountSnapshot> ReadAccountSnapshotAsync(bool forceRefreshVillages = false, bool preferCurrentPageVillages = false, bool restorePageAfterProfile = true, bool suppressEnsureUiSync = false, bool skipOverviewNavigation = false, CancellationToken cancellationToken = default);
    Task<VillageStatus> ReadVillageStatusAsync(IReadOnlyList<Village> knownVillages, IReadOnlyList<Building> knownBuildings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TroopTrainingQueueStatus>> ReadTroopTrainingQueuesAsync(IReadOnlyList<Building>? knownBuildings = null, CancellationToken cancellationToken = default);
    Task<int?> RefreshAdventureCountAsync(bool forceReload = true, CancellationToken cancellationToken = default);
}

public sealed partial class TravianClient : IPostLoginSnapshotClient
{
}

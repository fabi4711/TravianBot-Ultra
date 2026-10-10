namespace TbotUltra.Core.Travian;

public enum ExpansionBuildingType
{
    Residence = 25,
    Palace = 26,
    CommandCenter = 44,
}

public sealed record SettlerTrainingPlan(
    int ExpansionSlots,
    int FreeSlots,
    int SettlersAvailable,
    int SettlersToTrain,
    string Reason);

public sealed record VillageFoundingState(
    int SettlersAtHome,
    int FreeExpansionSlots,
    long CulturePoints,
    long CulturePointsNeededForNextVillage,
    IReadOnlyDictionary<string, long> Resources,
    bool TargetFieldIsFree);

public sealed record VillageFoundingReadiness(bool IsReady, IReadOnlyList<string> MissingRequirements);

/// <summary>
/// Expansion rules for settlers and founding new villages. One expansion slot takes 3 settlers; the
/// settlers carry 750 of each resource to the new village.
/// </summary>
public static class ExpansionCalculator
{
    public const int SettlersPerVillage = 3;
    public const int FoundingResourcesEach = 750;

    private static readonly IReadOnlyDictionary<ExpansionBuildingType, int[]> SlotLevels =
        new Dictionary<ExpansionBuildingType, int[]>
        {
            [ExpansionBuildingType.Residence] = [10, 20],
            [ExpansionBuildingType.Palace] = [10, 15, 20],
            [ExpansionBuildingType.CommandCenter] = [10, 15, 20],
        };

    private static readonly string[] ResourceKeys = ["wood", "clay", "iron", "crop"];

    public static int ExpansionSlots(ExpansionBuildingType building, int level)
        => SlotLevels[building].Count(required => level >= required);

    /// <summary>
    /// Settlers still needed to fill every free expansion slot. Settlers at home, in training and on the way
    /// all count as available, so a run never trains more than the slots can use.
    /// </summary>
    public static SettlerTrainingPlan PlanSettlerTraining(
        ExpansionBuildingType building,
        int buildingLevel,
        int usedSlots,
        int settlersAtHome,
        int settlersInTraining,
        int settlersOnTheWay)
    {
        var slots = ExpansionSlots(building, buildingLevel);
        var freeSlots = Math.Max(0, slots - Math.Max(0, usedSlots));
        var available = Math.Max(0, settlersAtHome) + Math.Max(0, settlersInTraining) + Math.Max(0, settlersOnTheWay);
        if (slots == 0)
        {
            return new SettlerTrainingPlan(0, 0, available, 0, $"{building} level {buildingLevel} has no expansion slot (needs level {SlotLevels[building][0]}).");
        }

        if (freeSlots == 0)
        {
            return new SettlerTrainingPlan(slots, 0, available, 0, $"All {slots} expansion slot(s) of {building} are used.");
        }

        var toTrain = Math.Max(0, freeSlots * SettlersPerVillage - available);
        var reason = toTrain == 0
            ? $"{available} settler(s) already cover {freeSlots} free slot(s)."
            : $"{freeSlots} free slot(s) need {freeSlots * SettlersPerVillage} settler(s); {available} available.";
        return new SettlerTrainingPlan(slots, freeSlots, available, toTrain, reason);
    }

    public static VillageFoundingReadiness EvaluateFounding(VillageFoundingState state)
    {
        var missing = new List<string>();
        if (state.SettlersAtHome < SettlersPerVillage)
        {
            missing.Add($"settlers at home {state.SettlersAtHome}/{SettlersPerVillage}");
        }

        if (state.FreeExpansionSlots <= 0)
        {
            missing.Add("no free expansion slot");
        }

        if (state.CulturePoints < state.CulturePointsNeededForNextVillage)
        {
            missing.Add($"culture points {state.CulturePoints}/{state.CulturePointsNeededForNextVillage}");
        }

        foreach (var key in ResourceKeys)
        {
            var amount = state.Resources.TryGetValue(key, out var value) ? value : 0;
            if (amount < FoundingResourcesEach)
            {
                missing.Add($"{key} {amount}/{FoundingResourcesEach}");
            }
        }

        if (!state.TargetFieldIsFree)
        {
            missing.Add("target field is not a free village field");
        }

        return new VillageFoundingReadiness(missing.Count == 0, missing);
    }
}

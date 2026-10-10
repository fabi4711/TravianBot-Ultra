using TbotUltra.Core.Travian;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ExpansionCalculatorTests
{
    [Theory]
    [InlineData(ExpansionBuildingType.Residence, 9, 0)]
    [InlineData(ExpansionBuildingType.Residence, 10, 1)]
    [InlineData(ExpansionBuildingType.Residence, 19, 1)]
    [InlineData(ExpansionBuildingType.Residence, 20, 2)]
    [InlineData(ExpansionBuildingType.Palace, 10, 1)]
    [InlineData(ExpansionBuildingType.Palace, 15, 2)]
    [InlineData(ExpansionBuildingType.Palace, 20, 3)]
    [InlineData(ExpansionBuildingType.CommandCenter, 15, 2)]
    public void ExpansionSlots_FollowBuildingLevel(ExpansionBuildingType building, int level, int expected)
    {
        Assert.Equal(expected, ExpansionCalculator.ExpansionSlots(building, level));
    }

    [Fact]
    public void PlanSettlerTraining_TrainsThreeForOneFreeSlot()
    {
        var plan = ExpansionCalculator.PlanSettlerTraining(ExpansionBuildingType.Residence, 10, 0, 0, 0, 0);

        Assert.Equal(1, plan.FreeSlots);
        Assert.Equal(3, plan.SettlersToTrain);
    }

    [Fact]
    public void PlanSettlerTraining_CountsSettlersAtHomeInTrainingAndOnTheWay()
    {
        var plan = ExpansionCalculator.PlanSettlerTraining(ExpansionBuildingType.Palace, 15, 0, 2, 1, 3);

        Assert.Equal(2, plan.FreeSlots);
        Assert.Equal(6, plan.SettlersAvailable);
        Assert.Equal(0, plan.SettlersToTrain);
    }

    [Fact]
    public void PlanSettlerTraining_TrainsNothingBelowLevelTen()
    {
        var plan = ExpansionCalculator.PlanSettlerTraining(ExpansionBuildingType.Residence, 9, 0, 0, 0, 0);

        Assert.Equal(0, plan.SettlersToTrain);
        Assert.Contains("needs level 10", plan.Reason);
    }

    [Fact]
    public void PlanSettlerTraining_TrainsNothingWhenSlotsAreUsed()
    {
        var plan = ExpansionCalculator.PlanSettlerTraining(ExpansionBuildingType.Residence, 10, 1, 0, 0, 0);

        Assert.Equal(0, plan.FreeSlots);
        Assert.Equal(0, plan.SettlersToTrain);
    }

    [Fact]
    public void EvaluateFounding_ReadyWhenEveryRequirementIsMet()
    {
        var readiness = ExpansionCalculator.EvaluateFounding(State());

        Assert.True(readiness.IsReady);
        Assert.Empty(readiness.MissingRequirements);
    }

    [Fact]
    public void EvaluateFounding_ListsEveryMissingRequirement()
    {
        var readiness = ExpansionCalculator.EvaluateFounding(State() with
        {
            SettlersAtHome = 2,
            FreeExpansionSlots = 0,
            CulturePoints = 400,
            Resources = Resources(clay: 749),
            TargetFieldIsFree = false,
        });

        Assert.False(readiness.IsReady);
        Assert.Equal(
            ["settlers at home 2/3", "no free expansion slot", "culture points 400/500", "clay 749/750", "target field is not a free village field"],
            readiness.MissingRequirements);
    }

    private static VillageFoundingState State() => new(3, 1, 500, 500, Resources(), true);

    private static IReadOnlyDictionary<string, long> Resources(long clay = 750)
        => new Dictionary<string, long> { ["wood"] = 750, ["clay"] = clay, ["iron"] = 750, ["crop"] = 750 };
}

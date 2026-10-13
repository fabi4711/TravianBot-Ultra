using Microsoft.Extensions.Configuration;
using TbotUltra.Core.Configuration;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ResourceTransferPayloadApplierTests
{
    [Fact]
    public void Apply_MapsAndNormalizesWholeResourceTransferDomain()
    {
        var payload = new Dictionary<string, string>
        {
            [BotOptionPayloadKeys.ResourceTransferEnabled] = "true",
            [BotOptionPayloadKeys.ResourceTransferTargetVillageName] = "Target",
            [BotOptionPayloadKeys.ResourceTransferAdditionalTargetVillageNames] = "[\"Second\",\"Third\",\"second\"]",
            [BotOptionPayloadKeys.ResourceTransferSourceVillageNames] = "One,Two,one",
            [BotOptionPayloadKeys.ResourceTransferSourceThresholdPercent] = "101",
            [BotOptionPayloadKeys.ResourceTransferSourceKeepPercent] = "100",
            [BotOptionPayloadKeys.ResourceTransferTargetFillPercent] = "-1",
            [BotOptionPayloadKeys.ResourceTransferSendWood] = "false",
            [BotOptionPayloadKeys.ResourceTransferSendClay] = "false",
            [BotOptionPayloadKeys.ResourceTransferSendIron] = "true",
            [BotOptionPayloadKeys.ResourceTransferSendCrop] = "true",
        };

        var result = BotOptionsPayloadApplier.Apply(new BotOptions(), payload);

        Assert.True(result.ResourceTransferEnabled);
        Assert.Equal("Target", result.ResourceTransferTargetVillageName);
        Assert.Equal(new[] { "Second", "Third" }, result.ResourceTransferAdditionalTargetVillageNames);
        Assert.Equal(new[] { "One", "Two" }, result.ResourceTransferSourceVillageNames);
        Assert.Equal(100, result.ResourceTransferSourceThresholdPercent);
        Assert.Equal(99, result.ResourceTransferSourceKeepPercent);
        Assert.Equal(0, result.ResourceTransferTargetFillPercent);
        Assert.False(result.ResourceTransferSendWood);
        Assert.False(result.ResourceTransferSendClay);
        Assert.True(result.ResourceTransferSendIron);
        Assert.True(result.ResourceTransferSendCrop);
    }

    [Fact]
    public void Apply_InvalidAndEmptyValuesPreserveSource()
    {
        var source = new BotOptions
        {
            ResourceTransferEnabled = true,
            ResourceTransferSourceThresholdPercent = 75,
            ResourceTransferSourceVillageNames = ["Existing"],
        };
        var payload = new Dictionary<string, string>
        {
            [BotOptionPayloadKeys.ResourceTransferEnabled] = "invalid",
            [BotOptionPayloadKeys.ResourceTransferSourceThresholdPercent] = "invalid",
            [BotOptionPayloadKeys.ResourceTransferSourceVillageNames] = " ",
        };

        var result = BotOptionsPayloadApplier.Apply(source, payload);

        Assert.True(result.ResourceTransferEnabled);
        Assert.Equal(75, result.ResourceTransferSourceThresholdPercent);
        Assert.Equal(new[] { "Existing" }, result.ResourceTransferSourceVillageNames);
    }

    [Theory]
    [InlineData("Kornkammer", new[] { "Rohstoffhof", "Bollwerk" }, new[] { "Kornkammer", "Rohstoffhof", "Bollwerk" })]
    [InlineData("Kornkammer", new string[0], new[] { "Kornkammer" })]
    [InlineData("Kornkammer", new[] { "kornkammer", " ", "Bollwerk" }, new[] { "Kornkammer", "Bollwerk" })]
    [InlineData("", new[] { "Rohstoffhof" }, new[] { "Rohstoffhof" })]
    public void ResolveTargetNames_KeepsPrimaryFirstAndDropsEmptyOrDuplicateAdditionalTargets(
        string target,
        string[] additionalTargets,
        string[] expected)
    {
        var options = new BotOptions
        {
            ResourceTransferTargetVillageName = target,
            ResourceTransferAdditionalTargetVillageNames = additionalTargets.ToList(),
        };

        Assert.Equal(expected, TbotUltra.Worker.Services.TravianClient.ResolveResourceTransferTargetNames(options));
    }

    [Fact]
    public void Apply_MigratesLegacySecondTargetWhenNoTargetListIsGiven()
    {
        var payload = new Dictionary<string, string>
        {
            [BotOptionPayloadKeys.ResourceTransferSecondTargetVillageName] = " Bollwerk ",
        };

        var result = BotOptionsPayloadApplier.Apply(new BotOptions(), payload);

        Assert.Equal(new[] { "Bollwerk" }, result.ResourceTransferAdditionalTargetVillageNames);
    }

    [Fact]
    public void Apply_EmptyTargetListWinsOverLegacySecondTarget()
    {
        var payload = new Dictionary<string, string>
        {
            [BotOptionPayloadKeys.ResourceTransferAdditionalTargetVillageNames] = "[]",
            [BotOptionPayloadKeys.ResourceTransferSecondTargetVillageName] = "Bollwerk",
        };

        var result = BotOptionsPayloadApplier.Apply(new BotOptions(), payload);

        Assert.Empty(result.ResourceTransferAdditionalTargetVillageNames);
    }

    [Fact]
    public void FromConfiguration_MigratesLegacySecondTargetAndPrefersTargetList()
    {
        var legacy = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BotOptionPayloadKeys.ResourceTransferSecondTargetVillageName] = "Bollwerk",
            })
            .Build();
        var migrated = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BotOptionPayloadKeys.ResourceTransferSecondTargetVillageName] = string.Empty,
                [$"{BotOptionPayloadKeys.ResourceTransferAdditionalTargetVillageNames}:0"] = "Bollwerk",
                [$"{BotOptionPayloadKeys.ResourceTransferAdditionalTargetVillageNames}:1"] = "Kornkammer",
            })
            .Build();

        Assert.Equal(new[] { "Bollwerk" }, BotOptionsFactory.FromConfiguration(legacy).ResourceTransferAdditionalTargetVillageNames);
        Assert.Equal(new[] { "Bollwerk", "Kornkammer" }, BotOptionsFactory.FromConfiguration(migrated).ResourceTransferAdditionalTargetVillageNames);
    }

    [Fact]
    public void Apply_ParsesJsonVillageNameContainingComma()
    {
        var payload = new Dictionary<string, string>
        {
            [BotOptionPayloadKeys.ResourceTransferSourceVillageNames] = "[\"1 for all, all for 1\"]",
        };

        var result = BotOptionsPayloadApplier.Apply(new BotOptions(), payload);

        Assert.Equal(["1 for all, all for 1"], result.ResourceTransferSourceVillageNames);
    }
}

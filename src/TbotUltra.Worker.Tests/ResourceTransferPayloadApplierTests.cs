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
            [BotOptionPayloadKeys.ResourceTransferSecondTargetVillageName] = "Second",
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
        Assert.Equal("Second", result.ResourceTransferSecondTargetVillageName);
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
    [InlineData("Kornkammer", "Rohstoffhof", new[] { "Kornkammer", "Rohstoffhof" })]
    [InlineData("Kornkammer", "", new[] { "Kornkammer" })]
    [InlineData("Kornkammer", "kornkammer", new[] { "Kornkammer" })]
    [InlineData("", "Rohstoffhof", new[] { "Rohstoffhof" })]
    public void ResolveTargetNames_KeepsPrimaryFirstAndDropsEmptyOrDuplicateSecondTarget(
        string target,
        string secondTarget,
        string[] expected)
    {
        var options = new BotOptions
        {
            ResourceTransferTargetVillageName = target,
            ResourceTransferSecondTargetVillageName = secondTarget,
        };

        Assert.Equal(expected, TbotUltra.Worker.Services.TravianClient.ResolveResourceTransferTargetNames(options));
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

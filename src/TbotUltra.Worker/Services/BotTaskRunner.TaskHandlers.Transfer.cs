using TbotUltra.Worker.Services.Automation;

namespace TbotUltra.Worker.Services;

public sealed partial class BotTaskRunner
{
    private static async Task ExecuteSendResourcesBetweenVillagesAsync(TaskExecutionContext context)
    {
        context.Log("send_resources_between_villages: starting.");
        var result = await new CombatOperation(context.Client)
            .SendResourcesBetweenOwnVillagesAsync(context.CancellationToken);
        context.Log(result);
        ThrowIfTaskBlocked("send_resources_between_villages", result);
    }

    private static async Task ExecuteCreateMarketplaceCropOffersAsync(TaskExecutionContext context)
    {
        context.Log("create_marketplace_crop_offers: starting.");
        var result = await context.Client.CreateMarketplaceCropOffersAsync(context.CancellationToken);
        context.Log(result);
        ThrowIfTaskBlocked("create_marketplace_crop_offers", result);
    }

    private static async Task ExecuteSendReinforcementsBetweenVillagesAsync(TaskExecutionContext context)
    {
        context.Log("send_reinforcements_between_villages: starting.");
        var result = await new CombatOperation(context.Client)
            .SendReinforcementsBetweenOwnVillagesAsync(context.CancellationToken);
        context.Log(result);
        ThrowIfTaskBlocked("send_reinforcements_between_villages", result);
    }
}

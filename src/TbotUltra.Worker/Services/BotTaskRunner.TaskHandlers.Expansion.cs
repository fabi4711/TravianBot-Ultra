using System.Globalization;
using System.Text.RegularExpressions;

namespace TbotUltra.Worker.Services;

public sealed partial class BotTaskRunner
{
    private static async Task ExecuteTrainSettlersAsync(TaskExecutionContext context)
    {
        var result = await context.Client.TrainSettlersAsync(context.CancellationToken);
        context.Log(result.Message);
        context.RecordTaskResult("train_settlers", result.Message);
        if (!result.Completed)
        {
            ThrowExpansionWait(result);
        }
    }

    private static async Task ExecuteFoundVillageAsync(TaskExecutionContext context)
    {
        if (!TryParseFoundingTarget(context.Options.TargetBuildingSlotOrName, out var x, out var y))
        {
            context.Log($"Task 'found_village' requires target_building_slot_or_name as 'xy:X|Y' (got '{context.Options.TargetBuildingSlotOrName}').");
            return;
        }

        var result = await context.Client.FoundVillageAsync(x, y, context.CancellationToken);
        context.Log(result.Message);
        context.RecordTaskResult("found_village", result.Message);
        if (!result.Completed)
        {
            ThrowExpansionWait(result);
        }
    }

    // A result without a wait is a hard failure (e.g. field taken); the queue then retries or fails it.
    private static void ThrowExpansionWait(ExpansionStepResult result)
    {
        if (result.WaitSeconds is int seconds)
        {
            throw new TaskWaitException(seconds, $"{result.Message} queue_wait_seconds={seconds}", TaskWaitReasons.WorkQueued);
        }

        throw new InvalidOperationException(result.Message);
    }

    internal static bool TryParseFoundingTarget(string? value, out int x, out int y)
    {
        x = 0;
        y = 0;
        var match = Regex.Match(value ?? string.Empty, @"^\s*(?:xy:)?\(?\s*(-?\d+)\s*[|/]\s*(-?\d+)\s*\)?\s*$");
        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out x)
            && int.TryParse(match.Groups[2].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out y);
    }
}

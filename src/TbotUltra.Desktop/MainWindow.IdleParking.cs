using TbotUltra.Core.Configuration;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private async Task MaybeParkOnDorf2Async(
        BotOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await _botService.TryParkOnDorf2IfOnDorf1Async(
                    options,
                    AppendLog,
                    cancellationToken))
            {
                AppendLog("[dorf2-park] moved the current village from Dorf1 to Dorf2 during automation.");
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppendLog($"[dorf2-park] could not move from Dorf1 to Dorf2; automation will continue: {ex.Message}");
        }
    }
}

using Microsoft.Playwright;

namespace TbotUltra.Worker.Infrastructure;

internal static class BrowserClickActions
{
    public static async Task ClickViewportPointAsync(
        IPage page,
        BrowserTraceLogger traceLogger,
        float x,
        float y,
        string action,
        CancellationToken cancellationToken)
    {
        using var trace = traceLogger.BeginOperation("CLICK", action, "verified viewport point");
        try
        {
            await page.Mouse.ClickAsync(x, y).WaitAsync(cancellationToken);
            trace.Complete("success");
        }
        catch (OperationCanceledException)
        {
            trace.Complete("canceled");
            throw;
        }
        catch (Exception ex)
        {
            trace.Complete("failed", $"{ex.GetType().Name}: {ex.Message}");
            throw;
        }
    }
}

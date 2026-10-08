using System.Reflection;
using System.Windows;
using System.Windows.Headless;
using System.Windows.Threading;
using TbotUltra.Desktop;

namespace TbotUltra.Web;

/// <summary>
/// Runs the desktop application on the headless UI dispatcher: the same App startup and MainWindow the
/// Windows build shows, minus the pixels. Stopping the web host closes the main window, which runs the
/// app's normal shutdown path (stopping automation, saving state).
/// </summary>
public sealed class DesktopHost : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DesktopHost> _logger;

    public DesktopHost(IHostApplicationLifetime lifetime, ILogger<DesktopHost> logger)
    {
        _lifetime = lifetime;
        _logger = logger;
    }

    public MainWindow? MainWindow { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Application.HeadlessShutdownRequested += exitCode =>
        {
            _logger.LogInformation("Desktop app requested shutdown (exit code {ExitCode}).", exitCode);
            _lifetime.StopApplication();
        };

        Dispatcher.Main.UnhandledException += (_, e) =>
            _logger.LogError(e.Exception, "Unhandled exception on the UI dispatcher.");

        return Dispatcher.Main.InvokeAsync(() =>
        {
            var app = new App();
            app.InitializeComponent();
            typeof(Application)
                .GetMethod("OnStartup", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(app, [new StartupEventArgs()]);

            MainWindow = new MainWindow();
            app.MainWindow = MainWindow;
            MainWindow.Show();
            _logger.LogInformation("Desktop main window is running headless.");
        }).Task;
    }

    /// <summary>
    /// Closes open dialogs, then the main window, and waits for the app's own shutdown (stopping the
    /// automation, closing the browser, saving state) so a container stop does not lose data.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (MainWindow is not { IsClosed: false } window)
        {
            return;
        }

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await Dispatcher.Main.InvokeAsync(() =>
        {
            window.Closed += (_, _) => closed.TrySetResult();
            HeadlessDialogs.IsAttended = static () => false;
            foreach (var dialog in HeadlessWindows.Open.Where(open => !ReferenceEquals(open, window)).Reverse())
            {
                // Like the user cancelling it; only a dialog that refuses to close is torn down.
                dialog.Close();
                if (!dialog.IsClosed)
                {
                    dialog.HeadlessForceClose();
                }
            }
        }).Task.WaitAsync(cancellationToken);

        // Close from a fresh dispatcher turn, after the modal frames above have unwound.
        _ = Dispatcher.Main.InvokeAsync(window.Close);
        try
        {
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(25), cancellationToken);
            _logger.LogInformation("Desktop app shut down cleanly.");
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Desktop app did not finish shutting down in time.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Host shutdown timeout reached before the desktop app finished.");
        }
    }
}

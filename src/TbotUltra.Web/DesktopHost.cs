using System.Reflection;
using System.Windows;
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

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (MainWindow is { IsClosed: false } window)
        {
            await Dispatcher.Main.InvokeAsync(window.Close).Task.WaitAsync(cancellationToken);
        }
    }
}

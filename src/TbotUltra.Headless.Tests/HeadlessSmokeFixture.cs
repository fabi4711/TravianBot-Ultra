using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace TbotUltra.Desktop.Tests;

/// <summary>
/// Headless stand-in for the desktop suite's WPF fixture: runs each smoke action on the headless UI
/// dispatcher with one Application instance, so window and panel construction is exercised on Linux.
/// </summary>
public sealed class WpfSmokeFixture : IDisposable
{
    public WpfSmokeFixture()
    {
        Dispatcher.Main.Invoke(() =>
        {
            if (Application.Current is null)
            {
                _ = new Application();
            }
        });
    }

    public void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        Dispatcher.Main.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        failure?.Throw();
    }

    public void Dispose()
    {
    }
}

[CollectionDefinition(Name)]
public sealed class WpfSmokeCollection : ICollectionFixture<WpfSmokeFixture>
{
    public const string Name = "wpf-smoke";
}

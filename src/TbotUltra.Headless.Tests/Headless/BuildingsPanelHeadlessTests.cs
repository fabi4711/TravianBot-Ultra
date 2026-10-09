using System.Windows;
using System.Windows.Controls;
using System.Windows.Headless;
using TbotUltra.Desktop.Models;
using TbotUltra.Desktop.Views;
using Xunit;

namespace TbotUltra.Desktop.Tests.Headless;

[Collection(WpfSmokeCollection.Name)]
public sealed class BuildingsPanelHeadlessTests
{
    private readonly WpfSmokeFixture _wpf;

    public BuildingsPanelHeadlessTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    // MainWindow resolves its view models from App.Services, which App.OnStartup builds.
    internal static void EnsureAppServices()
    {
        // A fresh data root seeded like a release download, so the test never touches repo config.
        var repoRoot = TbotUltra.Worker.ProjectRootLocator.FindProjectRoot(AppContext.BaseDirectory);
        var dataRoot = Path.Combine(Path.GetTempPath(), "tbot-headless-tests");
        Directory.CreateDirectory(Path.Combine(dataRoot, "config"));
        foreach (var file in Directory.GetFiles(Path.Combine(repoRoot, ".release-template", "config")))
        {
            File.Copy(file, Path.Combine(dataRoot, "config", Path.GetFileName(file)), overwrite: true);
        }

        File.Copy(Path.Combine(repoRoot, ".release-template", "env.template"), Path.Combine(dataRoot, ".env"), overwrite: true);
        Environment.SetEnvironmentVariable(TbotUltra.Worker.ProjectRootLocator.ProjectRootVariable, dataRoot);

        if (App.Services is not null)
        {
            return;
        }

        var configure = typeof(App).GetMethod("ConfigureServices", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        typeof(App).GetProperty(nameof(App.Services))!.SetValue(null, configure.Invoke(null, null));
    }

    [Fact]
    public void LoadedSlots_AppearAsSlotButtonsInTheMainWindow()
    {
        _wpf.Run(() =>
        {
            EnsureAppServices();
            var window = new MainWindow();
            try
            {
                var panel = HeadlessXaml.Descendants(window).OfType<BuildingsPanel>().Single();
                var slotLists = HeadlessXaml.Descendants(panel).OfType<ItemsControl>()
                    .Where(list => list.ItemTemplate is not null)
                    .ToList();
                Assert.Equal(2, slotLists.Count);

                var before = slotLists.Sum(list => list.Items.Count);
                window.BuildingsVm.BuildingSlots.Add(new BuildingSlotRow { SlotId = 26, Name = "Main Building", Level = 5, Gid = 15 });
                window.BuildingsVm.BuildingSlots.Add(new BuildingSlotRow { SlotId = 19, Name = "Warehouse", Level = 3, Gid = 10 });
                window.BuildingsVm.BuildingSlots.Add(new BuildingSlotRow { SlotId = 20 });

                Assert.Equal(before + 3, slotLists.Sum(list => list.Items.Count));
                var buttons = slotLists
                    .SelectMany(list => HeadlessTemplates.RealizeItems(list))
                    .SelectMany(item => HeadlessXaml.Descendants(item).OfType<Button>())
                    .ToList();
                Assert.Equal(before + 3, buttons.Count);
                var texts = slotLists
                    .SelectMany(list => HeadlessTemplates.RealizeItems(list))
                    .SelectMany(item => HeadlessXaml.Descendants(item).OfType<TextBlock>())
                    .Select(text => text.Text)
                    .ToList();
                Assert.Contains(texts, text => text.Contains("Main Building", StringComparison.Ordinal));
            }
            finally
            {
                window.HeadlessForceClose();
            }
        });
    }
}

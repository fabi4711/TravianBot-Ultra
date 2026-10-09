using System.Windows.Controls;
using System.Windows.Headless;
using TbotUltra.Desktop.Views;
using Xunit;

namespace TbotUltra.Desktop.Tests.Headless;

[Collection(WpfSmokeCollection.Name)]
public sealed class ResourcesPanelHeadlessTests
{
    private readonly WpfSmokeFixture _wpf;

    public ResourcesPanelHeadlessTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    [Fact]
    public void ResourcesPanel_InheritsTheResourcesViewModelThroughTheNestedTabControl()
    {
        _wpf.Run(() =>
        {
            BuildingsPanelHeadlessTests.EnsureAppServices();
            var window = new MainWindow();
            try
            {
                var panel = HeadlessXaml.Descendants(window).OfType<ResourcesPanel>().Single();
                Assert.Same(window.ResourcesVm, panel.DataContext);

                var lists = HeadlessXaml.Descendants(panel).OfType<ItemsControl>().Where(list => list.ItemTemplate is not null).ToList();
                Assert.Contains(lists, list => list.Items.Count == window.ResourcesVm.StorageBars.Count);
            }
            finally
            {
                window.HeadlessForceClose();
            }
        });
    }
}

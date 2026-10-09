using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Headless;
using Xunit;

namespace TbotUltra.Desktop.Tests.Headless;

[Collection(WpfSmokeCollection.Name)]
public sealed class HeadlessBindingTests
{
    private readonly WpfSmokeFixture _wpf;

    public HeadlessBindingTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    [Fact]
    public void ItemsSourceBoundToCollectionViewSource_ShowsTheFilteredView()
    {
        _wpf.Run(() =>
        {
            var items = new ObservableCollection<string> { "keep-1", "drop", "keep-2" };
            var viewSource = new CollectionViewSource { Source = items };
            viewSource.Filter += (_, e) => e.Accepted = ((string)e.Item).StartsWith("keep", StringComparison.Ordinal);
            var list = new ItemsControl();
            HeadlessBindings.SetBinding(list, nameof(ItemsControl.ItemsSource), new Binding { Source = viewSource });

            Assert.Equal(new object[] { "keep-1", "keep-2" }, list.Items.Cast<object>().ToArray());

            items.Add("keep-3");
            Assert.Equal(3, list.Items.Count);
        });
    }
}

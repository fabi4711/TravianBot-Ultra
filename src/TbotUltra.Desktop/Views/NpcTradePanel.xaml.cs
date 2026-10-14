using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class NpcTradePanel : UserControl
{
    public static readonly DependencyProperty SectionProperty = DependencyProperty.Register(
        nameof(Section),
        typeof(string),
        typeof(NpcTradePanel),
        new PropertyMetadata("All"));

    public string Section
    {
        get => (string)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    private MainWindow? _host;

    public NpcTradePanel()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplySection();
    }

    private void ApplySection()
    {
        if (string.Equals(Section, "All", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SectionSpacerColumn.Width = new GridLength(0);
        if (string.Equals(Section, "Trading", StringComparison.OrdinalIgnoreCase))
        {
            NpcSection.Visibility = Visibility.Collapsed;
            NpcColumn.Width = new GridLength(0);
            TradingColumn.Width = new GridLength(1, GridUnitType.Star);
            return;
        }

        TradingSection.Visibility = Visibility.Collapsed;
        TradingColumn.Width = new GridLength(0);
        NpcColumn.Width = new GridLength(1, GridUnitType.Star);
    }

    private MainWindow? Host => _host ??= Window.GetWindow(this) as MainWindow;

    internal TextBlock GoldSpent => NpcTradeGoldSpentTextBlock;
    internal TextBlock TroopsCount => NpcTradeTroopsTextBlock;
    internal TextBlock BuildingsCount => NpcTradeBuildingsTextBlock;
    internal ComboBox TransferTargetVillage => ResourceTransferTargetVillageComboBox;
    internal ItemsControl TransferAdditionalTargetVillages => ResourceTransferAdditionalTargetVillagesItemsControl;
    internal ItemsControl TransferSourceVillages => ResourceTransferSourceVillagesItemsControl;
    internal ComboBox SourceThreshold => ResourceTransferSourceThresholdComboBox;
    internal ComboBox SourceKeep => ResourceTransferSourceKeepComboBox;
    internal ComboBox TargetFill => ResourceTransferTargetFillComboBox;
    internal CheckBox TransferWood => ResourceTransferWoodCheckBox;
    internal CheckBox TransferClay => ResourceTransferClayCheckBox;
    internal CheckBox TransferIron => ResourceTransferIronCheckBox;
    internal CheckBox TransferCrop => ResourceTransferCropCheckBox;
    internal TextBlock TransferStatus => ResourceTransferStatusTextBlock;
    internal Button TransferQueueNow => ResourceTransferQueueNowButton;
    internal Button TransferScanVillages => ResourceTransferScanVillagesButton;
    internal CheckBox CropOfferEnabled => CropOfferEnabledCheckBox;
    internal ItemsControl CropOfferVillages => CropOfferVillagesItemsControl;
    internal ComboBox CropOfferThreshold => CropOfferThresholdComboBox;
    internal ComboBox CropOfferKeep => CropOfferKeepComboBox;
    internal ComboBox CropOfferRatio => CropOfferRatioComboBox;
    internal ComboBox CropOfferMaxOpen => CropOfferMaxOpenComboBox;

    private void ResourceTransferSetting_Changed(object sender, RoutedEventArgs e) => Host?.OnResourceTransferSettingChanged(sender, e);
    private void ResourceTransferSetting_SelectionChanged(object sender, SelectionChangedEventArgs e) => Host?.OnResourceTransferSettingSelectionChanged(sender, e);
    private void QueueResourceTransferNowButton_Click(object sender, RoutedEventArgs e) => Host?.OnQueueResourceTransferNowClicked(sender, e);
    private void ResourceTransferScanVillagesButton_Click(object sender, RoutedEventArgs e) => Host?.OnResourceTransferScanVillagesClicked(sender, e);
    private void CropOfferSetting_Changed(object sender, RoutedEventArgs e) => Host?.OnCropOfferSettingChanged();
    private void CropOfferSetting_SelectionChanged(object sender, SelectionChangedEventArgs e) => Host?.OnCropOfferSettingChanged();
}

using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop;

public partial class MainWindow
{
    private Views.NpcTradePanel NpcTradePanelControl => NpcTradeHubPanelControl.NpcPanel;
    private Views.NpcTradePanel ResourceTradingPanelControl => ResourcesHubPanelControl.TradingPanel;
    private TextBlock NpcTradeGoldSpentTextBlock => NpcTradePanelControl.GoldSpent;
    private TextBlock NpcTradeTroopsTextBlock => NpcTradePanelControl.TroopsCount;
    private TextBlock NpcTradeBuildingsTextBlock => NpcTradePanelControl.BuildingsCount;
    private ComboBox ResourceTransferTargetVillageComboBox => ResourceTradingPanelControl.TransferTargetVillage;
    private ItemsControl ResourceTransferAdditionalTargetVillagesItemsControl => ResourceTradingPanelControl.TransferAdditionalTargetVillages;
    private ItemsControl ResourceTransferSourceVillagesItemsControl => ResourceTradingPanelControl.TransferSourceVillages;
    private ComboBox ResourceTransferSourceThresholdComboBox => ResourceTradingPanelControl.SourceThreshold;
    private ComboBox ResourceTransferSourceKeepComboBox => ResourceTradingPanelControl.SourceKeep;
    private ComboBox ResourceTransferTargetFillComboBox => ResourceTradingPanelControl.TargetFill;
    private CheckBox ResourceTransferWoodCheckBox => ResourceTradingPanelControl.TransferWood;
    private CheckBox ResourceTransferClayCheckBox => ResourceTradingPanelControl.TransferClay;
    private CheckBox ResourceTransferIronCheckBox => ResourceTradingPanelControl.TransferIron;
    private CheckBox ResourceTransferCropCheckBox => ResourceTradingPanelControl.TransferCrop;
    private TextBlock ResourceTransferStatusTextBlock => ResourceTradingPanelControl.TransferStatus;
    private Button ResourceTransferQueueNowButton => ResourceTradingPanelControl.TransferQueueNow;
    private Button ResourceTransferScanVillagesButton => ResourceTradingPanelControl.TransferScanVillages;
    private CheckBox CropOfferEnabledCheckBox => ResourceTradingPanelControl.CropOfferEnabled;
    private ItemsControl CropOfferVillagesItemsControl => ResourceTradingPanelControl.CropOfferVillages;
    private ComboBox CropOfferThresholdComboBox => ResourceTradingPanelControl.CropOfferThreshold;
    private ComboBox CropOfferKeepComboBox => ResourceTradingPanelControl.CropOfferKeep;
    private ComboBox CropOfferRatioComboBox => ResourceTradingPanelControl.CropOfferRatio;
    private ComboBox CropOfferMaxOpenComboBox => ResourceTradingPanelControl.CropOfferMaxOpen;

    internal void OnResourceTransferSettingChanged(object sender, RoutedEventArgs e) => ResourceTransferSetting_Changed(sender, e);
    internal void OnResourceTransferSettingSelectionChanged(object sender, SelectionChangedEventArgs e) => ResourceTransferSetting_SelectionChanged(sender, e);
    internal void OnQueueResourceTransferNowClicked(object sender, RoutedEventArgs e) => QueueResourceTransferNowButton_Click(sender, e);
    internal void OnResourceTransferScanVillagesClicked(object sender, RoutedEventArgs e) => ResourceTransferScanVillagesButton_Click(sender, e);
    internal void OnCropOfferSettingChanged() => PersistCropOfferSettings();
}

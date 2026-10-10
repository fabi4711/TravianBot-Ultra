using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using TbotUltra.Desktop.ViewModels;
using TbotUltra.Desktop;
using TbotUltra.Desktop.Views;
using Xunit;

namespace TbotUltra.Desktop.Tests;

[Collection(WpfSmokeCollection.Name)]
public sealed class PanelWpfSmokeTests
{
    private readonly WpfSmokeFixture _wpf;

    public PanelWpfSmokeTests(WpfSmokeFixture wpf)
    {
        _wpf = wpf;
    }

    [Fact]
    public void MigratedPanels_LoadWithTheirViewModelAndCommandBindings()
    {
        _wpf.Run(() =>
        {
            AssertPanelCommands(new FarmingPanel(), new FarmListsViewModel(),
                "AnalyzeFarmListsButton", "FarmListSendAllNowButton", "CreateFarmListButton", "AddFarmsToListButton");
            AssertPanelCommands(new HeroPanel(), new HeroViewModel(),
                "RefreshAdventuresButton", "RefreshHeroHpButton", "RefreshHeroStatsButton", "RefreshHeroInventoryButton");
            AssertPanelCommands(new QueuePanel(), new TravianQueueViewModel(),
                "QueueRemoveButton", "QueueRedoButton", "QueueMoveToTopButton", "QueueMoveUpButton",
                "QueueMoveDownButton", "QueueMoveToBottomButton", "QueueRefreshButton",
                "ClearVillageQueueButton", "QueueClearButton", "QueuePopoutButton");
            AssertPanelCommands(new ResourcesPanel(), new ResourcesViewModel(), "LoadResourcesButton");
            AssertPanelCommands(new TroopsPanel(), new TroopTrainingViewModel(), "CheckCelebrationButton", "RefreshTroopQueuesButton");

            var hero = new HeroViewModel();
            var heroPanel = new HeroPanel { DataContext = hero };
            Layout(heroPanel);
            var refreshHero = Assert.IsType<Button>(heroPanel.FindName("RefreshHeroStatsButton"));
            Assert.True(refreshHero.IsEnabled);
            hero.SetManualOperationRunning(true);
            heroPanel.UpdateLayout();
            Assert.False(refreshHero.IsEnabled);
            hero.SetManualOperationRunning(false);
            heroPanel.UpdateLayout();
            Assert.True(refreshHero.IsEnabled);

            var buildings = new BuildingsViewModel();
            var buildingsPanel = new BuildingsPanel { DataContext = buildings };
            Layout(buildingsPanel);
            Assert.Same(buildings, buildingsPanel.DataContext);
            Assert.Contains(FindButtons(buildingsPanel), button => ReferenceEquals(button.Command, buildings.LoadCommand));
            Assert.Contains(FindButtons(buildingsPanel), button => ReferenceEquals(button.Command, buildings.UpgradeAllToMaxCommand));
        });
    }

    [Fact]
    public void ResourcesPanel_CheckboxesCommitTheVisibleSelectionToTheViewModel()
    {
        _wpf.Run(() =>
        {
            var viewModel = new ResourcesViewModel();
            viewModel.LoadResourceUpgradeTypes(["crop"]);
            var panel = new ResourcesPanel { DataContext = viewModel };
            Layout(panel);

            var checkBoxes = new[]
            {
                Assert.IsType<CheckBox>(panel.FindName("UpgradeWoodCheckBox")),
                Assert.IsType<CheckBox>(panel.FindName("UpgradeClayCheckBox")),
                Assert.IsType<CheckBox>(panel.FindName("UpgradeIronCheckBox")),
                Assert.IsType<CheckBox>(panel.FindName("UpgradeCropCheckBox")),
            };
            foreach (var checkBox in checkBoxes)
            {
                var binding = Assert.IsType<BindingExpression>(checkBox.GetBindingExpression(CheckBox.IsCheckedProperty));
                Assert.Equal(BindingMode.TwoWay, binding.ParentBinding.Mode);
                Assert.Equal(UpdateSourceTrigger.PropertyChanged, binding.ParentBinding.UpdateSourceTrigger);
                checkBox.IsChecked = true;
            }

            panel.CommitUpgradeTypeSelection();

            Assert.Equal(["wood", "clay", "iron", "crop"], viewModel.SelectedUpgradeTypes);
        });
    }

    [Fact]
    public void TroopsPanel_ShowsSavedTroopOutsideCurrentCatalogWithoutSelectingAnother()
    {
        _wpf.Run(() =>
        {
            var viewModel = new TroopTrainingViewModel();
            viewModel.Initialize();
            viewModel.UpdateTroopOptions("Romans");
            viewModel.Buildings[0].SelectedTroop = "Praetorian";
            var panel = new TroopsPanel { DataContext = viewModel };
            Layout(panel);

            viewModel.UpdateTroopOptions("Gauls");
            Layout(panel);
            var troopCombo = Assert.Single(FindComboBoxes(panel),
                combo => ReferenceEquals(combo.ItemsSource, viewModel.Buildings[0].TroopOptions));

            Assert.Equal("Praetorian", troopCombo.Text);
            Assert.Equal("Praetorian", viewModel.Buildings[0].SelectedTroop);
            Assert.DoesNotContain("Praetorian", troopCombo.Items.Cast<string>());

            troopCombo.SelectedItem = "Phalanx";
            Assert.Equal("Phalanx", viewModel.Buildings[0].SelectedTroop);
        });
    }

    [Fact]
    public void TroopsPanel_VillageSwitchLoadsEachSavedTroopBeforeRefreshingCatalog()
    {
        _wpf.Run(() =>
        {
            var viewModel = new TroopTrainingViewModel();
            viewModel.Initialize();
            viewModel.UpdateTroopOptions("Romans");
            viewModel.Buildings[0].SelectedTroop = "Praetorian";
            viewModel.Buildings[1].SelectedTroop = "Equites Imperatoris";
            viewModel.Buildings[2].SelectedTroop = "Fire Catapult";
            var firstVillage = viewModel.BuildVillageTrainingPayload();
            var secondVillage = firstVillage with
            {
                Barracks = firstVillage.Barracks with { TroopType = "Swordsman" },
                Stable = firstVillage.Stable with { TroopType = "Druidrider" },
                Workshop = firstVillage.Workshop with { TroopType = "Trebuchet" },
            };
            var panel = new TroopsPanel { DataContext = viewModel };
            Layout(panel);

            viewModel.ApplyVillageTrainingPayload(secondVillage);
            viewModel.UpdateTroopOptions("Gauls");
            Layout(panel);

            foreach (var (option, saved) in new[]
            {
                (viewModel.Buildings[0], "Swordsman"),
                (viewModel.Buildings[1], "Druidrider"),
                (viewModel.Buildings[2], "Trebuchet"),
            })
            {
                var combo = Assert.Single(FindComboBoxes(panel),
                    item => ReferenceEquals(item.ItemsSource, option.TroopOptions));
                Assert.Equal(saved, combo.Text);
                Assert.Equal(saved, option.SelectedTroop);
                Assert.Contains(saved, combo.Items.Cast<string>());
            }

            Assert.Equal("Swordsman", viewModel.BuildVillageTrainingPayload().Barracks.TroopType);
            Assert.Equal("Druidrider", viewModel.BuildVillageTrainingPayload().Stable.TroopType);
            Assert.Equal("Trebuchet", viewModel.BuildVillageTrainingPayload().Workshop.TroopType);
        });
    }

    [Fact]
    public void TroopSettingsWindow_ShowsSavedSelectionsWithoutOfferingWrongTribeTroops()
    {
        _wpf.Run(() =>
        {
            var viewModel = new TroopTrainingViewModel();
            viewModel.Initialize();
            viewModel.Buildings[0].SelectedTroop = "Clubswinger";
            viewModel.Buildings[1].SelectedTroop = "Paladin";
            viewModel.Buildings[2].SelectedTroop = "Catapult";
            var row = new TroopTrainingQuickVillageRow(
                "v1", "Village 1", true, viewModel.BuildVillageTrainingPayload(), "Romans");
            var window = new TroopTrainingOptionsWindow([row]);
            var content = Assert.IsAssignableFrom<FrameworkElement>(window.Content);
            content.Measure(new Size(980, 600));
            content.Arrange(new Rect(0, 0, 980, 600));
            content.UpdateLayout();

            foreach (var (option, saved) in new[]
            {
                (row.Barracks, "Clubswinger"),
                (row.Stable, "Paladin"),
                (row.Workshop, "Catapult"),
            })
            {
                var combo = Assert.Single(FindComboBoxes(content),
                    item => ReferenceEquals(item.ItemsSource, option.TroopOptions));
                Assert.Equal(saved, combo.Text);
                Assert.DoesNotContain(saved, combo.Items.Cast<string>());
            }

            Assert.Equal("Clubswinger", row.BuildPayload().Barracks.TroopType);
            Assert.Equal("Paladin", row.BuildPayload().Stable.TroopType);
            Assert.Equal("Catapult", row.BuildPayload().Workshop.TroopType);
        });
    }

    private static void AssertPanelCommands(UserControl panel, object viewModel, params string[] buttonNames)
    {
        panel.DataContext = viewModel;
        Layout(panel);
        Assert.Same(viewModel, panel.DataContext);

        foreach (var name in buttonNames)
        {
            var button = Assert.IsType<Button>(panel.FindName(name));
            Assert.NotNull(button.Command);
            Assert.Equal(button.Command.CanExecute(button.CommandParameter), button.IsEnabled);
        }
    }

    private static void Layout(UserControl panel)
    {
        panel.Measure(new Size(1280, 900));
        panel.Arrange(new Rect(0, 0, 1280, 900));
        panel.UpdateLayout();
    }

    private static IEnumerable<Button> FindButtons(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is Button button)
            {
                yield return button;
            }

            foreach (var nested in FindButtons(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<ComboBox> FindComboBoxes(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is ComboBox comboBox)
            {
                yield return comboBox;
            }

            foreach (var nested in FindComboBoxes(child))
            {
                yield return nested;
            }
        }
    }
}

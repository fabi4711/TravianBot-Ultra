using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace TbotUltra.Desktop.Views;

public partial class ExpansionPanel : UserControl
{
    public event Action? TrainSettlersRequested;
    public event Action<int, int>? FoundVillageRequested;

    public ExpansionPanel() => InitializeComponent();

    public void ShowStatus(string text) => ExpansionStatusText.Text = text;

    private void TrainSettlers_Click(object sender, RoutedEventArgs e) => TrainSettlersRequested?.Invoke();

    private void FoundVillage_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(FoundXTextBox.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(FoundYTextBox.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y))
        {
            ShowStatus("Enter whole-number X and Y coordinates.");
            return;
        }

        FoundVillageRequested?.Invoke(x, y);
    }
}

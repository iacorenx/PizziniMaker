using System.Windows;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public partial class CompleteDayWindow : Window
{
    public CompleteDayWindow(int totalMinutes, int missingMinutes)
    {
        InitializeComponent();
        MessageText.Text = $"Il totale è {ActivityCatalog.FormatDuration(totalMinutes)}. " +
            $"Mancano {ActivityCatalog.FormatDuration(missingMinutes)} per arrivare a 8 ore.";
    }

    public ActivityType? SelectedActivity { get; private set; }

    private void Qu_Click(object sender, RoutedEventArgs e) => Select(ActivityType.Qu);

    private void Leave_Click(object sender, RoutedEventArgs e) => Select(ActivityType.FeriePermesso);

    private void Select(ActivityType type)
    {
        SelectedActivity = type;
        DialogResult = true;
    }
}

using System.Windows;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public partial class OvertimeConfirmationWindow : Window
{
    public OvertimeConfirmationWindow(int totalMinutes, int overtimeMinutes)
    {
        InitializeComponent();
        MessageText.Text = $"Il totale è {ActivityCatalog.FormatDuration(totalMinutes)}. " +
            $"Sono presenti {ActivityCatalog.FormatDuration(overtimeMinutes)} di straordinario. Vuoi chiudere il giorno?";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}

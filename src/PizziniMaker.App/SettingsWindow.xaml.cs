using System.Windows;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings, bool initialSetup)
    {
        _settings = settings;
        InitializeComponent();
        EmployeeTextBox.Text = settings.EmployeeName;
        TrackingDatePicker.SelectedDate = settings.TrackingStartDate.ToDateTime(TimeOnly.MinValue);
        TopmostCheckBox.IsChecked = settings.AlwaysOnTop;
        StartupCheckBox.IsChecked = settings.LaunchAtStartup;

        if (initialSetup)
        {
            Title = "Benvenuto in PizziniMaker";
            EmployeeTextBox.Focus();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var employeeName = EmployeeTextBox.Text.Trim();
        if (employeeName.Length == 0)
        {
            MessageBox.Show(this, "Inserire il nome del dipendente.", "Impostazioni",
                MessageBoxButton.OK, MessageBoxImage.Information);
            EmployeeTextBox.Focus();
            return;
        }

        if (TrackingDatePicker.SelectedDate is not DateTime trackingDate)
        {
            MessageBox.Show(this, "Selezionare la data di inizio controllo.", "Impostazioni",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _settings.EmployeeName = employeeName;
        _settings.TrackingStartDate = DateOnly.FromDateTime(trackingDate);
        _settings.AlwaysOnTop = TopmostCheckBox.IsChecked == true;
        _settings.LaunchAtStartup = StartupCheckBox.IsChecked == true;
        DialogResult = true;
    }
}

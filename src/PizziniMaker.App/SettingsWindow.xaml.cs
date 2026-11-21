using System.Windows;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

/// <summary>Azioni immediate richiamabili dalle impostazioni (non legate a Salva/Annulla).</summary>
public interface ISettingsActions
{
    void ManageWorkCardHistory(Window owner);

    /// <summary>Esegue l'importazione; restituisce true se le impostazioni sono state modificate.</summary>
    bool ImportLegacyData(Window owner);
}

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly ISettingsActions? _actions;
    private List<CustomActivityDefinition> _customActivities;

    public SettingsWindow(AppSettings settings, bool initialSetup, ISettingsActions? actions = null)
    {
        _settings = settings;
        _actions = actions;
        _customActivities = settings.CustomActivities.Select(Clone).ToList();
        InitializeComponent();
        EmployeeTextBox.Text = settings.EmployeeName;
        TrackingDatePicker.SelectedDate = settings.TrackingStartDate.ToDateTime(TimeOnly.MinValue);
        TopmostCheckBox.IsChecked = settings.AlwaysOnTop;
        StartupCheckBox.IsChecked = settings.LaunchAtStartup;
        UpdateCustomActivitiesSummary();

        if (initialSetup)
        {
            Title = "Benvenuto in PizziniMaker";
            EmployeeTextBox.Focus();
        }
    }

    private void ManageActivities_Click(object sender, RoutedEventArgs e)
    {
        var window = new CustomActivitiesWindow(_customActivities) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _customActivities = window.SelectedActivities.ToList();
            UpdateCustomActivitiesSummary();
        }
    }

    private void ManageHistory_Click(object sender, RoutedEventArgs e) => _actions?.ManageWorkCardHistory(this);

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_actions is null || !_actions.ImportLegacyData(this))
        {
            return;
        }

        // L'importazione ha aggiornato le impostazioni salvate: le riporto nei campi senza perdere le modifiche in corso.
        foreach (var activity in _settings.CustomActivities.Where(activity =>
                     _customActivities.All(existing => !string.Equals(existing.Name, activity.Name, StringComparison.OrdinalIgnoreCase))))
        {
            _customActivities.Add(Clone(activity));
        }
        UpdateCustomActivitiesSummary();

        if (EmployeeTextBox.Text.Trim().Length == 0)
        {
            EmployeeTextBox.Text = _settings.EmployeeName;
        }
        var importedStart = _settings.TrackingStartDate.ToDateTime(TimeOnly.MinValue);
        if (TrackingDatePicker.SelectedDate is not DateTime selected || importedStart < selected)
        {
            TrackingDatePicker.SelectedDate = importedStart;
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
        _settings.CustomActivities = _customActivities.Select(Clone).ToList();
        DialogResult = true;
    }

    private void UpdateCustomActivitiesSummary() =>
        CustomActivitiesSummary.Text = _customActivities.Count == 0
            ? "Nessuna attività personalizzata configurata."
            : string.Join(" · ", _customActivities.Select(activity => $"{activity.Icon} {activity.Name}"));

    private static CustomActivityDefinition Clone(CustomActivityDefinition activity) => new()
    {
        Id = activity.Id,
        Name = activity.Name,
        Icon = activity.Icon
    };
}

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

    void ShowUpdateWindow(Core.ReleaseInfo? knownRelease = null);
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
        UpdatesCheckBox.IsChecked = settings.CheckUpdatesAtStartup;
        ShowKmRate(settings.KmRateCents);
        UpdateCustomActivitiesSummary();
        Title = $"Impostazioni - PizziniMaker {AppVersion.Display}";
        VersionText.Text = $"PizziniMaker {AppVersion.Display} · {AppVersion.Credits}";

        if (initialSetup)
        {
            Title = $"Benvenuto in PizziniMaker {AppVersion.Display}";
            EmployeeTextBox.Focus();
        }
    }

    private void ShowKmRate(int cents) =>
        KmRateTextBox.Text = cents > 0 ? (cents / 100m).ToString("0.00##") : string.Empty;

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

    private void CheckUpdates_Click(object sender, RoutedEventArgs e) => _actions?.ShowUpdateWindow();

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
        if (KmRateTextBox.Text.Trim().Length == 0)
        {
            ShowKmRate(_settings.KmRateCents);
        }
        TopmostCheckBox.IsChecked = _settings.AlwaysOnTop;
        StartupCheckBox.IsChecked = _settings.LaunchAtStartup;
        UpdatesCheckBox.IsChecked = _settings.CheckUpdatesAtStartup;
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

        var kmRateCents = 0L;
        if (KmRateTextBox.Text.Trim().Length > 0 &&
            (!ExpenseCalculator.TryParseEuro(KmRateTextBox.Text, out kmRateCents) || kmRateCents > 1000))
        {
            MessageBox.Show(this, "Indicare la tariffa €/km con un numero, es. 0,42.", "Impostazioni",
                MessageBoxButton.OK, MessageBoxImage.Information);
            KmRateTextBox.Focus();
            return;
        }

        _settings.EmployeeName = employeeName;
        _settings.KmRateCents = (int)kmRateCents;
        _settings.CheckUpdatesAtStartup = UpdatesCheckBox.IsChecked == true;
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

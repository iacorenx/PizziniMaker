using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public sealed record ExpenseItem(ExpenseEntry Entry)
{
    public string DateText => Entry.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public string CategoryText => $"{Entry.Category.GetIcon()} {Entry.Category switch
    {
        ExpenseCategory.Viaggio => "Viaggio",
        ExpenseCategory.Chilometri => "Km auto",
        var other => other.ToDisplayName()
    }}";

    public string DescriptionText => Entry.Category == ExpenseCategory.Chilometri
        ? $"{ExpenseCalculator.FormatKm(Entry.Km)} km × {ActivityCatalog.FormatEuro(Entry.KmRateCents)}" +
          (Entry.Description.Length > 0 ? $" · {Entry.Description}" : string.Empty)
        : Entry.Description;

    public string AmountText => ActivityCatalog.FormatEuro(Entry.TotalCents);

    public string StatusText => Entry.Status == ExpenseStatus.Rimborsata
        ? Entry.ReimbursedOn is { } date ? $"Rimborsata {date:dd/MM}" : "Rimborsata"
        : "Da rimborsare";

    public string StatusBrush => Entry.Status == ExpenseStatus.Rimborsata ? "#2F6F4E" : "#A74716";

    public string AttachmentMark => Entry.HasAttachment ? "📎" : string.Empty;
}

public partial class ExpensesWindow : Window
{
    private readonly ExpenseStore _store;
    private readonly DateOnly _selectedDay;
    private readonly Func<int> _kmRateCents;
    private DateOnly _month;

    public ExpensesWindow(ExpenseStore store, DateOnly selectedDay, Func<int> kmRateCents)
    {
        _store = store;
        _selectedDay = selectedDay;
        _kmRateCents = kmRateCents;
        _month = new DateOnly(selectedDay.Year, selectedDay.Month, 1);
        InitializeComponent();
        DayOnlyText.Text = $"Solo {ActivityCatalog.FormatDate(selectedDay)}";
        Refresh();
    }

    private void Refresh(Guid? select = null)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        MonthText.Text = culture.TextInfo.ToTitleCase(_month.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture));
        var dayOnly = DayOnlyCheckBox.IsChecked == true;
        DayOnlyCheckBox.IsEnabled = _month.Year == _selectedDay.Year && _month.Month == _selectedDay.Month;

        IReadOnlyList<ExpenseEntry> entries;
        try
        {
            entries = _store.LoadMonth(_month.Year, _month.Month);
        }
        catch (InvalidDataException exception)
        {
            entries = [];
            MessageBox.Show(this, $"Impossibile leggere le note spese del mese: {exception.Message}", "Note spese",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var visible = dayOnly && DayOnlyCheckBox.IsEnabled ? entries.Where(entry => entry.Date == _selectedDay).ToArray() : entries;
        var items = visible.Select(entry => new ExpenseItem(entry)).ToArray();
        ExpensesList.ItemsSource = items;
        ExpensesList.SelectedItem = items.FirstOrDefault(item => item.Entry.Id == select);

        var summary = ExpenseCalculator.Summarize(visible);
        var scope = dayOnly && DayOnlyCheckBox.IsEnabled ? "Giorno" : "Mese";
        SummaryText.Text = summary.Count == 0
            ? $"{scope}: nessuna nota spese."
            : $"{scope}: {summary.Count} {(summary.Count == 1 ? "spesa" : "spese")} · totale {ActivityCatalog.FormatEuro(summary.TotalCents)}\n" +
              $"Da rimborsare: {ActivityCatalog.FormatEuro(summary.ToReimburseCents)} · Rimborsato: {ActivityCatalog.FormatEuro(summary.ReimbursedCents)}" +
              (summary.Km > 0 ? $" · {ExpenseCalculator.FormatKm(summary.Km)} km" : string.Empty);
        UpdateButtons();
    }

    private ExpenseEntry? Selected => (ExpensesList.SelectedItem as ExpenseItem)?.Entry;

    private void UpdateButtons()
    {
        var selected = Selected;
        EditButton.IsEnabled = DeleteButton.IsEnabled = ToggleStatusButton.IsEnabled = selected is not null;
        OpenAttachmentButton.IsEnabled = selected?.HasAttachment == true;
        ToggleStatusButton.Content = selected?.Status == ExpenseStatus.Rimborsata ? "Segna da rimborsare" : "Segna rimborsata";
    }

    private void ExpensesList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            Refresh();
        }
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(-1);
        Refresh();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(1);
        Refresh();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var defaultDate = _month.Year == _selectedDay.Year && _month.Month == _selectedDay.Month ? _selectedDay : _month;
        var window = new ExpenseEditWindow(_store, null, defaultDate, _kmRateCents()) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _month = new DateOnly(window.Result.Date.Year, window.Result.Date.Month, 1);
            Refresh(window.Result.Id);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void ExpensesList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (Selected is not { } selected)
        {
            return;
        }
        var window = new ExpenseEditWindow(_store, selected, selected.Date, _kmRateCents()) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _month = new DateOnly(window.Result.Date.Year, window.Result.Date.Month, 1);
            Refresh(window.Result.Id);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } selected)
        {
            return;
        }
        var attachment = selected.HasAttachment ? "\nAnche la copia dell'allegato verrà eliminata." : string.Empty;
        if (MessageBox.Show(this, $"Eliminare la spesa del {selected.Date:dd/MM/yyyy} di {ActivityCatalog.FormatEuro(selected.TotalCents)}?{attachment}",
                "Note spese", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        Run(() => _store.Delete(selected));
        Refresh();
    }

    private void ToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } selected)
        {
            return;
        }
        var status = selected.Status == ExpenseStatus.Rimborsata ? ExpenseStatus.DaRimborsare : ExpenseStatus.Rimborsata;
        Run(() => _store.SetStatus(selected, status));
        Refresh(selected.Id);
    }

    private void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } selected && _store.GetAttachmentPath(selected) is { } path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show(this, "L'allegato non è stato trovato.", "Note spese", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(_store.ExpenseDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_store.ExpenseDirectory}\"") { UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, $"Operazione non riuscita: {exception.Message}", "Note spese",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

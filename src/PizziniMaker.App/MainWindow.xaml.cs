using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PizziniMaker.Core;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void PreviousDay_Click(object sender, RoutedEventArgs e) =>
        ChangeDate(_viewModel.CurrentDate.AddDays(-1));

    private void NextDay_Click(object sender, RoutedEventArgs e) =>
        ChangeDate(_viewModel.CurrentDate.AddDays(1));

    private void ChangeDate(DateOnly date)
    {
        try
        {
            if (!_viewModel.ChangeDate(date))
            {
                ShowError(_viewModel.LastError);
            }
        }
        catch (Exception exception)
        {
            ShowError($"Impossibile aprire la giornata: {exception.Message}");
        }
    }

    private void AddEntry_Click(object sender, RoutedEventArgs e) => _viewModel.AddEntry();

    private void RemoveEntry_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ActivityEntryViewModel entry })
        {
            _viewModel.RemoveEntry(entry);
        }
    }

    private void ConfirmDay_Click(object sender, RoutedEventArgs e)
    {
        var validationErrors = _viewModel.ValidateEntries();
        if (validationErrors.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, validationErrors));
            return;
        }

        var assessment = _viewModel.GetClosureAssessment();
        if (assessment.Kind == DayClosureKind.BelowRequired)
        {
            var completionWindow = new CompleteDayWindow(assessment.TotalMinutes, assessment.MissingMinutes) { Owner = this };
            if (completionWindow.ShowDialog() != true || completionWindow.SelectedActivity is not ActivityType completionType)
            {
                return;
            }

            if (!_viewModel.CompleteDayWith(completionType))
            {
                ShowError(_viewModel.LastError);
                return;
            }
        }
        else if (assessment.Kind is DayClosureKind.Overtime or DayClosureKind.OptionalDay)
        {
            var overtimeWindow = new OvertimeConfirmationWindow(assessment) { Owner = this };
            if (overtimeWindow.ShowDialog() != true)
            {
                return;
            }
        }
        else
        {
            var travelText = assessment.TravelMinutes > 0
                ? $" (di cui {ActivityCatalog.FormatDuration(assessment.TravelMinutes)} di viaggio)"
                : string.Empty;
            var answer = MessageBox.Show(this,
                $"Confermi l'acquisizione di {ActivityCatalog.FormatDuration(assessment.TotalMinutes)}{travelText} per questo giorno? " +
                "Le attività diventeranno di sola lettura.",
                "Conferma giorno", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var errors = _viewModel.ConfirmDay();
        if (errors.Count > 0)
        {
            ShowError(string.Join(Environment.NewLine, errors));
            return;
        }

        MessageBox.Show(this, "Giorno acquisito e log testuale generato.", "PizziniMaker",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ReopenDay_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Vuoi riaprire il giorno? Il log confermato verrà rimosso e potrai modificare nuovamente le attività.",
            "Riapri giorno", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes && !_viewModel.ReopenDay())
        {
            ShowError(_viewModel.LastError);
        }
    }

    private void ToggleDelivered_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.DeliveryActionText == "Togli consegnato" &&
            MessageBox.Show(this,
                "Togliere il contrassegno di consegnato? Il giorno tornerà fra quelli da stampare.",
                "Consegna", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        if (!_viewModel.ToggleDelivered())
        {
            ShowError(_viewModel.LastError);
        }
    }

    private void ForceNonWorkingDay_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Vuoi abilitare la compilazione solo per questo giorno non lavorativo? La scelta verrà salvata nella giornata.",
            "Forza apertura giorno", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes && !_viewModel.ForceNonWorkingDay())
        {
            ShowError(_viewModel.LastError);
        }
    }

    private void Calendar_Click(object sender, RoutedEventArgs e)
    {
        var calendar = new MonthlyCalendarWindow(_viewModel) { Owner = this };
        if (calendar.ShowDialog() == true && calendar.SelectedDate is DateOnly selectedDate)
        {
            ChangeDate(selectedDate);
        }
    }

    private void Expenses_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveCurrentDay();
        new ExpensesWindow(new ExpenseStore(_viewModel.Store), _viewModel.CurrentDate, () => _viewModel.Settings.KmRateCents)
        {
            Owner = this
        }.ShowDialog();
        _viewModel.RefreshExpenses();
    }

    private void Print_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).ShowPrintWindow(this);

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).ShowSettings(this);

    private void MissingDays_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var window = new MissingDaysWindow(_viewModel.GetMissingDays()) { Owner = this };
            if (window.ShowDialog() == true && window.SelectedDate is DateOnly date)
            {
                ChangeDate(date);
            }
        }
        catch (Exception exception)
        {
            ShowError($"Impossibile leggere i giorni mancanti: {exception.Message}");
        }
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e) =>
        ((App)Application.Current).OpenLogFolder();

    private void WorkCard_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private static bool IsAllowedSuffix(object sender, string text) =>
        sender is FrameworkElement { DataContext: ActivityEntryViewModel { Type: ActivityType.Mandato } }
            ? text.All(char.IsAsciiLetter)
            : text.All(char.IsAsciiDigit);

    private void ReferenceSuffix_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !IsAllowedSuffix(sender, e.Text);

    private void Digits_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(typeof(string)) is not string text || !text.Trim().All(char.IsAsciiDigit))
        {
            e.CancelCommand();
        }
    }

    private void ReferenceSuffix_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(typeof(string)) is not string text || !IsAllowedSuffix(sender, text.Trim()))
        {
            e.CancelCommand();
        }
    }

    private void Comment_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ActivityEntryViewModel entry })
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                entry.MoveSuggestionSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                entry.MoveSuggestionSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                e.Handled = entry.ApplySelectedSuggestion();
                break;
            case Key.Tab:
                // Tab applica il suggerimento evidenziato ma lascia proseguire il cambio di campo.
                entry.ApplySelectedSuggestion();
                break;
            case Key.Escape:
                if (entry.IsSuggestionOpen)
                {
                    entry.CloseSuggestions();
                    e.Handled = true;
                }
                break;
        }
    }

    private void Comment_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ActivityEntryViewModel entry })
        {
            entry.CloseSuggestions();
        }
    }

    private void Suggestion_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: WorkCardHistoryEntry suggestion } item &&
            ItemsControl.ItemsControlFromItemContainer(item)?.DataContext is ActivityEntryViewModel entry)
        {
            entry.ApplySuggestion(suggestion);
            e.Handled = true;
        }
    }

    private void CloseAllSuggestions()
    {
        foreach (var entry in _viewModel.Entries)
        {
            entry.CloseSuggestions();
        }
    }

    private void Window_LocationChanged(object? sender, EventArgs e) => CloseAllSuggestions();

    private void Window_Deactivated(object? sender, EventArgs e) => CloseAllSuggestions();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _viewModel.SaveCurrentDay();
        if (((App)Application.Current).IsExiting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Warning);
}

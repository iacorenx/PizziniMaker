using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        var answer = MessageBox.Show(this,
            "Confermi l'acquisizione delle ore di questo giorno? Le attività diventeranno di sola lettura.",
            "Conferma giorno", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
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

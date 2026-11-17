using System.Windows;
using System.Windows.Controls;
using PizziniMaker.Core;
using Button = System.Windows.Controls.Button;

namespace PizziniMaker.App;

public partial class MonthlyCalendarWindow : Window
{
    private readonly MonthlyCalendarViewModel _viewModel;
    private readonly MainViewModel _mainViewModel;

    public MonthlyCalendarWindow(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _viewModel = new MonthlyCalendarViewModel(mainViewModel);
        DataContext = _viewModel;
        InitializeComponent();
    }

    public DateOnly? SelectedDate { get; private set; }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e) => _viewModel.PreviousMonth();

    private void NextMonth_Click(object sender, RoutedEventArgs e) => _viewModel.NextMonth();

    private void Day_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CalendarCellViewModel { Date: DateOnly date } })
        {
            SelectedDate = date;
            DialogResult = true;
        }
    }

    private void MissingDays_Click(object sender, RoutedEventArgs e)
    {
        var window = new MissingDaysWindow(_mainViewModel.GetMissingDays()) { Owner = this };
        if (window.ShowDialog() == true && window.SelectedDate is DateOnly date)
        {
            SelectedDate = date;
            DialogResult = true;
        }
    }
}

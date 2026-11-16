using System.Windows;
using System.Windows.Input;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public sealed record MissingDayItem(DateOnly Date, string Label);

public partial class MissingDaysWindow : Window
{
    public MissingDaysWindow(IEnumerable<DateOnly> dates)
    {
        InitializeComponent();
        DaysList.ItemsSource = dates
            .Select(date => new MissingDayItem(date, ActivityCatalog.FormatDate(date)))
            .ToArray();
        DaysList.SelectedIndex = DaysList.Items.Count > 0 ? 0 : -1;
    }

    public DateOnly? SelectedDate { get; private set; }

    private void OpenSelectedDay()
    {
        if (DaysList.SelectedItem is not MissingDayItem item)
        {
            return;
        }

        SelectedDate = item.Date;
        DialogResult = true;
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelectedDay();

    private void DaysList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelectedDay();
}

using System.Collections.ObjectModel;
using System.Windows;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public partial class CustomActivitiesWindow : Window
{
    private readonly ObservableCollection<CustomActivityDefinition> _activities;

    public CustomActivitiesWindow(IEnumerable<CustomActivityDefinition> activities)
    {
        InitializeComponent();
        _activities = new ObservableCollection<CustomActivityDefinition>(activities.Select(Clone));
        ActivitiesList.ItemsSource = _activities;
        ActivitiesList.SelectedIndex = _activities.Count > 0 ? 0 : -1;
    }

    public IReadOnlyList<CustomActivityDefinition> SelectedActivities => _activities.Select(Clone).ToArray();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var window = new CustomActivityEditWindow(null, _activities.Select(item => item.Name)) { Owner = this };
        if (window.ShowDialog() == true)
        {
            _activities.Add(window.Activity);
            ActivitiesList.SelectedItem = window.Activity;
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (ActivitiesList.SelectedItem is not CustomActivityDefinition selected)
        {
            return;
        }

        var otherNames = _activities.Where(item => item.Id != selected.Id).Select(item => item.Name);
        var window = new CustomActivityEditWindow(selected, otherNames) { Owner = this };
        if (window.ShowDialog() == true)
        {
            var index = _activities.IndexOf(selected);
            _activities[index] = window.Activity;
            ActivitiesList.SelectedItem = window.Activity;
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ActivitiesList.SelectedItem is not CustomActivityDefinition selected)
        {
            return;
        }

        var answer = MessageBox.Show(this, $"Eliminare {selected.Name} dall'elenco delle attività disponibili?",
            "Attività personalizzate", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            _activities.Remove(selected);
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private static CustomActivityDefinition Clone(CustomActivityDefinition activity) => new()
    {
        Id = activity.Id,
        Name = activity.Name,
        Icon = activity.Icon
    };
}

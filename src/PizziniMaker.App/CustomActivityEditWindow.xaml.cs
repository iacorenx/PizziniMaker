using System.Windows;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public partial class CustomActivityEditWindow : Window
{
    private static readonly string[] Icons = ["✈️", "🎓", "🚗", "🚆", "🏨", "💼", "🛠️", "📌", "📚", "🤝", "🏭", "🧾"];
    private static readonly string[] ReservedNames = ["SCHEDA", "QU", "FERIE/PERMESSO", "MALATTIA", "INFORTUNIO"];
    private readonly string[] _otherNames;

    public CustomActivityEditWindow(CustomActivityDefinition? activity, IEnumerable<string> otherNames)
    {
        InitializeComponent();
        _otherNames = otherNames.Select(name => name.ToUpperInvariant()).ToArray();
        Activity = activity is null
            ? new CustomActivityDefinition()
            : new CustomActivityDefinition { Id = activity.Id, Name = activity.Name, Icon = activity.Icon };
        IconComboBox.ItemsSource = Icons;
        IconComboBox.SelectedItem = Icons.Contains(Activity.Icon) ? Activity.Icon : Icons[0];
        NameTextBox.Text = Activity.Name;
        NameTextBox.Focus();
    }

    public CustomActivityDefinition Activity { get; }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim().ToUpperInvariant();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Inserire il nome dell'attività.", "Attività personalizzata");
            return;
        }
        if (ReservedNames.Contains(name) || _otherNames.Contains(name))
        {
            MessageBox.Show(this, "Esiste già un'attività con questo nome.", "Attività personalizzata");
            return;
        }

        Activity.Name = name;
        Activity.Icon = IconComboBox.SelectedItem as string ?? "📌";
        DialogResult = true;
    }
}

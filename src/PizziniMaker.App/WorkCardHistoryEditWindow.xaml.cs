using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public sealed record WorkCardTypeOption(ActivityType Type, string DisplayName);

public partial class WorkCardHistoryEditWindow : Window
{
    private static readonly WorkCardTypeOption[] TypeOptions =
    [
        new(ActivityType.Scheda, "Scheda"),
        new(ActivityType.Mandato, "Mandato"),
        new(ActivityType.Preventivo, "Preventivo")
    ];

    public WorkCardHistoryEditWindow(ActivityType type, string? number, string? comment)
    {
        InitializeComponent();
        TypeComboBox.ItemsSource = TypeOptions;
        TypeComboBox.SelectedItem = TypeOptions.FirstOrDefault(option => option.Type == type) ?? TypeOptions[0];
        Title = number is null ? "Nuovo riferimento" : $"Modifica {ActivityCatalog.GetWorkCardReference(type, number)}";
        NumberTextBox.Text = number ?? (type == ActivityType.Preventivo ? $"/{DateTime.Today.Year}" : string.Empty);
        CommentTextBox.Text = comment ?? string.Empty;
        if (number is null)
        {
            NumberTextBox.Focus();
        }
        else
        {
            CommentTextBox.Focus();
            CommentTextBox.SelectAll();
        }
    }

    public ActivityType Type => (TypeComboBox.SelectedItem as WorkCardTypeOption)?.Type ?? ActivityType.Scheda;

    public string Number { get; private set; } = string.Empty;

    public string Comment { get; private set; } = string.Empty;

    private void Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        NumberLabel.Text = Type switch
        {
            ActivityType.Mandato => "Numero mandato (numero/lettera, es. 123/X)",
            ActivityType.Preventivo => "Numero preventivo (numero/anno, es. 1234/2026)",
            _ => "Numero scheda (4 cifre)"
        };
    }

    private void Number_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(character => char.IsAsciiDigit(character) ||
            Type != ActivityType.Scheda && (character == '/' || char.IsAsciiLetter(character)));

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var number = NumberTextBox.Text.Trim().ToUpperInvariant();
        if (!WorkDayValidator.IsValidWorkCardNumber(Type, number))
        {
            var error = WorkDayValidator.GetWorkCardFormatError(Type);
            MessageBox.Show(this, char.ToUpperInvariant(error[0]) + error[1..], "Storico",
                MessageBoxButton.OK, MessageBoxImage.Information);
            NumberTextBox.Focus();
            return;
        }

        Number = number;
        Comment = WorkCardHistory.NormalizeComment(CommentTextBox.Text);
        DialogResult = true;
    }
}

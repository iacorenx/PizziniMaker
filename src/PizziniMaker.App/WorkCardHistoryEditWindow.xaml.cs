using System.Windows;
using System.Windows.Input;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public partial class WorkCardHistoryEditWindow : Window
{
    public WorkCardHistoryEditWindow(string? number, string? comment)
    {
        InitializeComponent();
        Title = number is null ? "Nuova scheda" : $"Modifica scheda {number}";
        NumberTextBox.Text = number ?? string.Empty;
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

    public string Number { get; private set; } = string.Empty;

    public string Comment { get; private set; } = string.Empty;

    private void Number_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = !e.Text.All(char.IsAsciiDigit);

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var number = NumberTextBox.Text.Trim();
        if (!WorkDayValidator.IsValidWorkCardNumber(number))
        {
            MessageBox.Show(this, "Il numero di scheda deve contenere esattamente 4 cifre.", "Scheda",
                MessageBoxButton.OK, MessageBoxImage.Information);
            NumberTextBox.Focus();
            return;
        }

        Number = number;
        Comment = WorkCardHistory.NormalizeComment(CommentTextBox.Text);
        DialogResult = true;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

public sealed record WorkCardHistoryItem(WorkCardHistoryEntry Entry)
{
    public string Number => Entry.Reference;

    public string CommentText => string.IsNullOrWhiteSpace(Entry.Comment) ? "(senza commento)" : Entry.Comment;

    public string UsageText
    {
        get
        {
            var uses = Entry.UseCount == 1 ? "1 utilizzo" : $"{Entry.UseCount} utilizzi";
            return Entry.LastUsed is DateOnly lastUsed
                ? $"Ultimo uso: {lastUsed:dd/MM/yyyy} · {uses}"
                : "Inserita manualmente";
        }
    }
}

public partial class WorkCardHistoryWindow : Window
{
    private readonly WorkCardHistory _history;

    public WorkCardHistoryWindow(WorkCardHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        InitializeComponent();
        _history = new WorkCardHistory { Entries = history.Entries.Select(entry => entry.Clone()).ToList() };
        Refresh();
        SearchTextBox.Focus();
    }

    /// <summary>Storico modificato, valido solo se la finestra è stata confermata.</summary>
    public WorkCardHistory Result => _history;

    private void Refresh(WorkCardHistoryEntry? select = null)
    {
        var query = SearchTextBox.Text.Trim();
        var entries = query.Length == 0 ? _history.OrderedByRecent() : _history.Search(query, int.MaxValue);
        var items = entries.Select(entry => new WorkCardHistoryItem(entry)).ToArray();
        EntriesList.ItemsSource = items;
        EntriesList.SelectedItem = items.FirstOrDefault(item => ReferenceEquals(item.Entry, select)) ?? items.FirstOrDefault();
        CountText.Text = _history.Entries.Count == 1 ? "1 voce" : $"{_history.Entries.Count} voci";
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            Refresh();
        }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var window = new WorkCardHistoryEditWindow(ActivityType.Scheda, null, null) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (_history.Find(window.Type, window.Number) is { } existing &&
            MessageBox.Show(this, $"{existing.Reference} è già nello storico con il commento \"{existing.Comment}\". Sostituire il commento?",
                "Storico schede", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        Refresh(_history.Upsert(window.Type, window.Number, window.Comment));
    }

    private void Edit_Click(object sender, RoutedEventArgs e) => EditSelected();

    private void EntriesList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        if (EntriesList.SelectedItem is not WorkCardHistoryItem { Entry: var selected })
        {
            return;
        }

        var window = new WorkCardHistoryEditWindow(selected.Type, selected.Number, selected.Comment) { Owner = this };
        if (window.ShowDialog() != true)
        {
            return;
        }

        if (window.Type != selected.Type || window.Number != selected.Number)
        {
            if (_history.Find(window.Type, window.Number) is not null)
            {
                MessageBox.Show(this, $"{ActivityCatalog.GetWorkCardReference(window.Type, window.Number)} è già presente nello storico.",
                    "Storico schede", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            selected.Type = window.Type;
            selected.Number = window.Number;
        }

        selected.Comment = WorkCardHistory.NormalizeComment(window.Comment);
        Refresh(selected);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is not WorkCardHistoryItem { Entry: var selected })
        {
            return;
        }

        var answer = MessageBox.Show(this, $"Eliminare la scheda {selected.DisplayText} dallo storico?\n" +
            "Le giornate già salvate non vengono modificate.", "Storico schede", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            _history.Remove(selected.Type, selected.Number);
            Refresh();
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}

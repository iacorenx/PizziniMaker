using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace PizziniMaker.App;

public sealed record ExpenseCategoryOption(ExpenseCategory Category, string DisplayName);

public partial class ExpenseEditWindow : Window
{
    private readonly ExpenseStore _store;
    private readonly ExpenseEntry _entry;
    private readonly DateOnly? _originalDate;
    private readonly int _kmRateCents;
    private string? _pendingAttachment;
    private bool _removeAttachment;

    public ExpenseEditWindow(ExpenseStore store, ExpenseEntry? entry, DateOnly defaultDate, int kmRateCents)
    {
        _store = store;
        _entry = entry?.Clone() ?? new ExpenseEntry { Date = defaultDate };
        _originalDate = entry?.Date;
        _kmRateCents = entry is { Category: ExpenseCategory.Chilometri, KmRateCents: > 0 } ? entry.KmRateCents : kmRateCents;
        InitializeComponent();

        HeaderText.Text = entry is null ? "Nuova nota spese" : "Modifica nota spese";
        DatePicker.SelectedDate = _entry.Date.ToDateTime(TimeOnly.MinValue);
        var options = Enum.GetValues<ExpenseCategory>()
            .Select(category => new ExpenseCategoryOption(category, $"{category.GetIcon()} {category.ToDisplayName()}"))
            .ToArray();
        CategoryComboBox.ItemsSource = options;
        CategoryComboBox.SelectedItem = options.First(option => option.Category == _entry.Category);
        AmountTextBox.Text = _entry.Category != ExpenseCategory.Chilometri && _entry.AmountCents > 0
            ? (_entry.AmountCents / 100m).ToString("0.00")
            : string.Empty;
        KmTextBox.Text = _entry.Km > 0 ? ExpenseCalculator.FormatKm(_entry.Km) : string.Empty;
        DescriptionTextBox.Text = _entry.Description;
        ReimbursedCheckBox.IsChecked = _entry.Status == ExpenseStatus.Rimborsata;
        UpdateCategory();
        UpdateAttachment();
        AmountTextBox.Focus();
    }

    /// <summary>Spesa salvata (valida solo se la finestra è stata confermata).</summary>
    public ExpenseEntry Result => _entry;

    private ExpenseCategory SelectedCategory =>
        (CategoryComboBox.SelectedItem as ExpenseCategoryOption)?.Category ?? ExpenseCategory.Pasto;

    private void Category_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded || AmountLabel is not null)
        {
            UpdateCategory();
        }
    }

    private void UpdateCategory()
    {
        var isKm = SelectedCategory == ExpenseCategory.Chilometri;
        AmountLabel.Visibility = AmountTextBox.Visibility = isKm ? Visibility.Collapsed : Visibility.Visible;
        KmLabel.Visibility = KmPanel.Visibility = isKm ? Visibility.Visible : Visibility.Collapsed;
        UpdateKmText();
    }

    private void Km_TextChanged(object sender, TextChangedEventArgs e) => UpdateKmText();

    private void UpdateKmText()
    {
        if (KmRateText is null)
        {
            return;
        }
        if (_kmRateCents <= 0)
        {
            KmRateText.Text = "Tariffa €/km non impostata: indicarla in Impostazioni → Note spese.";
            return;
        }
        var rate = ActivityCatalog.FormatEuro(_kmRateCents);
        KmRateText.Text = ExpenseCalculator.TryParseKm(KmTextBox.Text, out var km) && km > 0
            ? $"{ExpenseCalculator.FormatKm(km)} km × {rate}/km = {ActivityCatalog.FormatEuro(ExpenseCalculator.GetKmAmountCents(km, _kmRateCents))}"
            : $"Tariffa: {rate}/km";
    }

    private void UpdateAttachment()
    {
        if (_pendingAttachment is not null)
        {
            AttachmentText.Text = $"Da allegare: {Path.GetFileName(_pendingAttachment)}";
        }
        else if (!_removeAttachment && _entry.HasAttachment)
        {
            AttachmentText.Text = Path.GetFileName(_entry.AttachmentFile);
        }
        else
        {
            AttachmentText.Text = "Nessun allegato (facoltativo)";
        }

        var hasAttachment = _pendingAttachment is not null || (!_removeAttachment && _entry.HasAttachment);
        OpenAttachmentButton.IsEnabled = hasAttachment;
        RemoveAttachmentButton.IsEnabled = hasAttachment;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Scegli la ricevuta o lo scontrino da allegare",
            Filter = "Ricevute (*.pdf;*.jpg;*.jpeg;*.png;*.heic)|*.pdf;*.jpg;*.jpeg;*.png;*.heic|Tutti i file (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            _pendingAttachment = dialog.FileName;
            _removeAttachment = false;
            UpdateAttachment();
        }
    }

    private void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        var path = _pendingAttachment ?? _store.GetAttachmentPath(_entry);
        if (path is null || !File.Exists(path))
        {
            MessageBox.Show(this, "L'allegato non è stato trovato.", "Nota spese", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        _pendingAttachment = null;
        _removeAttachment = true;
        UpdateAttachment();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DatePicker.SelectedDate is not DateTime date)
        {
            ShowInfo("Selezionare la data della spesa.");
            return;
        }

        _entry.Date = DateOnly.FromDateTime(date);
        _entry.Category = SelectedCategory;
        _entry.Description = DescriptionTextBox.Text.Trim();
        if (_entry.Category == ExpenseCategory.Chilometri)
        {
            if (!ExpenseCalculator.TryParseKm(KmTextBox.Text, out var km))
            {
                ShowInfo("Indicare i chilometri con un numero, es. 120 o 85,5.");
                return;
            }
            _entry.Km = km;
            _entry.KmRateCents = _kmRateCents;
            _entry.AmountCents = 0;
        }
        else
        {
            if (!ExpenseCalculator.TryParseEuro(AmountTextBox.Text, out var cents))
            {
                ShowInfo("Indicare l'importo in euro, es. 12,50.");
                return;
            }
            _entry.AmountCents = cents;
            _entry.Km = 0;
            _entry.KmRateCents = 0;
        }

        var wasReimbursed = _entry.Status == ExpenseStatus.Rimborsata;
        _entry.Status = ReimbursedCheckBox.IsChecked == true ? ExpenseStatus.Rimborsata : ExpenseStatus.DaRimborsare;
        if (_entry.Status == ExpenseStatus.Rimborsata && !wasReimbursed)
        {
            _entry.ReimbursedOn = DateOnly.FromDateTime(DateTime.Today);
        }

        var errors = ExpenseCalculator.Validate(_entry);
        if (errors.Count > 0)
        {
            ShowInfo(string.Join(Environment.NewLine, errors));
            return;
        }

        try
        {
            if (_removeAttachment)
            {
                _store.RemoveAttachment(_entry);
            }
            if (_pendingAttachment is not null)
            {
                _store.CopyAttachment(_entry, _pendingAttachment);
            }
            _store.Save(_entry, _originalDate);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, $"Impossibile salvare la nota spese: {exception.Message}", "Nota spese",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void ShowInfo(string message) =>
        MessageBox.Show(this, message, "Nota spese", MessageBoxButton.OK, MessageBoxImage.Information);
}

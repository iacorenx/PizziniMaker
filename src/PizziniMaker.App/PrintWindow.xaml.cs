using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;
using PrintDialog = System.Windows.Controls.PrintDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace PizziniMaker.App;

public sealed class PrintDayCell : ObservableObject
{
    private readonly Action _selectionChanged;
    private readonly ISet<DateOnly> _selection;

    public PrintDayCell(DateOnly? date, WorkDay? day, ISet<DateOnly> selection, Action selectionChanged)
    {
        Date = date;
        _selection = selection;
        _selectionChanged = selectionChanged;
        IsVisible = date is not null;
        IsEnabled = day is { Entries.Count: > 0 } && day.TotalMinutes > 0;
        IsConfirmed = day?.IsConfirmed == true;
        DayNumber = date?.Day.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Detail = !IsEnabled ? string.Empty : $"{ActivityCatalog.FormatHours(day!.TotalMinutes)} h";
        Background = !IsEnabled ? "#F4F2EE" : IsConfirmed ? "#DDF3DF" : "#FFF8E7";
        ToolTip = date is { } value
            ? ActivityCatalog.FormatDate(value) + (!IsEnabled ? "\nNessuna attività da stampare"
                : IsConfirmed ? "\nGiorno confermato" : "\nBozza non confermata (stampata con la scritta BOZZA)")
            : null;
    }

    public DateOnly? Date { get; }

    public bool IsVisible { get; }

    public bool IsEnabled { get; }

    public bool IsConfirmed { get; }

    public string DayNumber { get; }

    public string Detail { get; }

    public string Background { get; }

    public string? ToolTip { get; }

    public bool IsSelected
    {
        get => Date is { } date && _selection.Contains(date);
        set
        {
            if (Date is not { } date || !IsEnabled || IsSelected == value)
            {
                return;
            }
            if (value)
            {
                _selection.Add(date);
            }
            else
            {
                _selection.Remove(date);
            }
            OnPropertyChanged();
            _selectionChanged();
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsSelected));
}

/// <summary>Selezione dei giorni dal calendario e stampa/esportazione dei foglietti A6.</summary>
public partial class PrintWindow : Window
{
    private readonly PizziniStore _store;
    private readonly Func<string> _employeeName;
    private readonly SortedSet<DateOnly> _selection = [];
    private DateOnly _month;
    private List<PrintDayCell> _cells = [];

    public PrintWindow(PizziniStore store, Func<string> employeeName, DateOnly initialDate)
    {
        _store = store;
        _employeeName = employeeName;
        _month = new DateOnly(initialDate.Year, initialDate.Month, 1);
        InitializeComponent();
        LoadMonth();
    }

    private void LoadMonth()
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        MonthText.Text = culture.TextInfo.ToTitleCase(_month.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture));
        var leading = ((int)_month.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        _cells = [];
        for (var index = 0; index < 42; index++)
        {
            var date = _month.AddDays(index - leading);
            if (index < leading || date.Month != _month.Month)
            {
                _cells.Add(new PrintDayCell(null, null, _selection, UpdateSummary));
                continue;
            }

            WorkDay? day = null;
            try
            {
                day = _store.LoadExistingDay(date);
            }
            catch (InvalidDataException)
            {
                // Giornata non leggibile: non selezionabile.
            }
            _cells.Add(new PrintDayCell(date, day, _selection, UpdateSummary));
        }
        DaysGrid.ItemsSource = _cells;
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var pages = PrintSlipBuilder.CountPages(_selection.Count);
        SelectionText.Text = _selection.Count == 0
            ? "Nessun giorno selezionato."
            : $"{(_selection.Count == 1 ? "1 giorno selezionato" : $"{_selection.Count} giorni selezionati")} · " +
              $"{(pages == 1 ? "1 foglio A4" : $"{pages} fogli A4")}\n" +
              string.Join(", ", _selection.Take(20).Select(date => date.ToString("dd/MM", CultureInfo.InvariantCulture))) +
              (_selection.Count > 20 ? " …" : string.Empty);
        var hasSelection = _selection.Count > 0;
        PreviewButton.IsEnabled = PdfButton.IsEnabled = WordButton.IsEnabled = PrintButton.IsEnabled = hasSelection;
    }

    private IReadOnlyList<IReadOnlyList<PrintSlip>>? BuildPages()
    {
        var slips = new List<PrintSlip>();
        foreach (var date in _selection)
        {
            try
            {
                if (_store.LoadExistingDay(date) is { } day)
                {
                    slips.Add(PrintSlipBuilder.Build(day, _employeeName()));
                }
            }
            catch (InvalidDataException exception)
            {
                MessageBox.Show(this, $"Impossibile leggere il giorno {date:dd/MM/yyyy}: {exception.Message}", "Stampa",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }
        }
        return PrintSlipBuilder.Paginate(slips);
    }

    private void PreviousMonth_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(-1);
        LoadMonth();
    }

    private void NextMonth_Click(object sender, RoutedEventArgs e)
    {
        _month = _month.AddMonths(1);
        LoadMonth();
    }

    private void SelectConfirmed_Click(object sender, RoutedEventArgs e) => SetMonthSelection(cell => cell.IsConfirmed);

    private void ClearMonth_Click(object sender, RoutedEventArgs e) => SetMonthSelection(_ => false);

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        _selection.Clear();
        _cells.ForEach(cell => cell.Refresh());
        UpdateSummary();
    }

    private void SetMonthSelection(Func<PrintDayCell, bool> selected)
    {
        foreach (var cell in _cells.Where(cell => cell.Date is not null && cell.IsEnabled))
        {
            if (selected(cell))
            {
                _selection.Add(cell.Date!.Value);
            }
            else
            {
                _selection.Remove(cell.Date!.Value);
            }
            cell.Refresh();
        }
        UpdateSummary();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (BuildPages() is not { } pages)
        {
            return;
        }
        var viewer = new DocumentViewer { Document = SlipRenderer.CreateFixedDocument(pages) };
        viewer.Loaded += (_, _) => viewer.FitToMaxPagesAcross(1);
        new Window
        {
            Title = "Anteprima di stampa - PizziniMaker",
            Owner = this,
            Width = 720,
            Height = 900,
            MinWidth = 400,
            MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Icon = Icon,
            Content = viewer
        }.ShowDialog();
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (BuildPages() is not { } pages)
        {
            return;
        }
        var dialog = new PrintDialog();
        try
        {
            dialog.PrintTicket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
            dialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
        }
        catch (Exception exception) when (exception is PrintQueueException or InvalidOperationException)
        {
            // Stampante senza impostazioni predefinite: restano quelle del driver.
        }
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        try
        {
            dialog.PrintDocument(SlipRenderer.CreateFixedDocument(pages).DocumentPaginator, "PizziniMaker - giorni");
        }
        catch (Exception exception) when (exception is PrintQueueException or InvalidOperationException)
        {
            MessageBox.Show(this, $"Stampa non riuscita: {exception.Message}", "Stampa", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SavePdf_Click(object sender, RoutedEventArgs e) =>
        Export("PDF (*.pdf)|*.pdf", ".pdf", SlipRenderer.SavePdf);

    private void SaveWord_Click(object sender, RoutedEventArgs e) =>
        Export("Documento Word (*.docx)|*.docx", ".docx", SlipRenderer.SaveWord);

    private void Export(string filter, string extension, Action<IReadOnlyList<IReadOnlyList<PrintSlip>>, string> save)
    {
        if (BuildPages() is not { } pages)
        {
            return;
        }
        var first = _selection.Min;
        var last = _selection.Max;
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            DefaultExt = extension,
            AddExtension = true,
            FileName = first == last
                ? $"Pizzini {first:yyyy-MM-dd}{extension}"
                : $"Pizzini {first:yyyy-MM-dd} - {last:yyyy-MM-dd}{extension}",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        try
        {
            save(pages, dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            MessageBox.Show(this, $"Salvataggio non riuscito: {exception.Message}", "Esportazione",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (MessageBox.Show(this, $"File salvato:\n{dialog.FileName}\n\nAprirlo ora?", "Esportazione",
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

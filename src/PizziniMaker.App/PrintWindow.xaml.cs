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
        IsEnabled = PrintDelivery.HasPrintableContent(day);
        IsConfirmed = day?.IsConfirmed == true;
        Delivery = PrintDelivery.GetStatus(day);
        NeedsPrinting = PrintDelivery.NeedsPrinting(day);
        DayNumber = date?.Day.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        Detail = !IsEnabled ? string.Empty : $"{ActivityCatalog.FormatHours(day!.TotalMinutes)} h";
        Status = !IsEnabled ? string.Empty
            : Delivery == DeliveryStatus.Delivered ? "✓ dato"
            : Delivery == DeliveryStatus.ChangedAfterDelivery ? "ristampa"
            : IsConfirmed ? "stampare" : "bozza";
        StatusBrush = Delivery == DeliveryStatus.ChangedAfterDelivery ? "#A74716"
            : Delivery == DeliveryStatus.Delivered ? "#5E6B60" : "#256B3A";
        Background = !IsEnabled ? "#F4F2EE"
            : Delivery == DeliveryStatus.Delivered ? "#E4E8E3"
            : Delivery == DeliveryStatus.ChangedAfterDelivery ? "#FCE4D6"
            : IsConfirmed ? "#DDF3DF" : "#FFF8E7";
        if (date is { } value)
        {
            var tip = ActivityCatalog.FormatDate(value);
            if (!IsEnabled)
            {
                tip += "\nNessuna attività da stampare";
            }
            else
            {
                tip += IsConfirmed ? "\nGiorno confermato" : "\nBozza non confermata (stampata con la scritta BOZZA)";
                tip += Delivery switch
                {
                    DeliveryStatus.Delivered => $"\nConsegnato il {day!.DeliveredAt!.Value.ToLocalTime():dd/MM/yyyy HH:mm}",
                    DeliveryStatus.ChangedAfterDelivery =>
                        $"\nConsegnato il {day!.DeliveredAt!.Value.ToLocalTime():dd/MM/yyyy HH:mm}, poi modificato: da ristampare",
                    _ => IsConfirmed ? "\nNon ancora consegnato" : string.Empty
                };
            }
            ToolTip = tip;
        }
    }

    public DeliveryStatus Delivery { get; }

    public bool NeedsPrinting { get; }

    public string Status { get; } = string.Empty;

    public string StatusBrush { get; }

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
        UpdateOtherMonthsNotice();
        UpdateSummary();
    }

    private void UpdateOtherMonthsNotice()
    {
        IReadOnlyList<DateOnly> toPrint;
        try
        {
            toPrint = _store.GetDaysToPrint();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            toPrint = [];
        }
        var others = toPrint.Where(date => date.Year != _month.Year || date.Month != _month.Month).ToArray();
        if (others.Length == 0)
        {
            OtherMonthsNotice.Visibility = Visibility.Collapsed;
            return;
        }
        var culture = CultureInfo.GetCultureInfo("it-IT");
        var months = others
            .Select(date => new DateOnly(date.Year, date.Month, 1))
            .Distinct()
            .Select(month => $"{month.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture)} ({others.Count(date => date.Year == month.Year && date.Month == month.Month)})");
        OtherMonthsText.Text = (others.Length == 1 ? "1 giorno confermato da stampare" : $"{others.Length} giorni confermati da stampare") +
            " in altri mesi: " + string.Join(", ", months);
        OtherMonthsNotice.Visibility = Visibility.Visible;
    }

    private string _lastAction = string.Empty;

    private void UpdateSummary()
    {
        var pages = PrintSlipBuilder.CountPages(_selection.Count);
        SelectionText.Text = _selection.Count == 0
            ? (_lastAction.Length > 0 ? _lastAction + "\n" : string.Empty) + "Nessun giorno selezionato."
            : $"{(_selection.Count == 1 ? "1 giorno selezionato" : $"{_selection.Count} giorni selezionati")} · " +
              $"{(pages == 1 ? "1 foglio A4" : $"{pages} fogli A4")}\n" +
              string.Join(", ", _selection.Take(20).Select(date => date.ToString("dd/MM", CultureInfo.InvariantCulture))) +
              (_selection.Count > 20 ? " …" : string.Empty);
        var hasSelection = _selection.Count > 0;
        PreviewButton.IsEnabled = PdfButton.IsEnabled = WordButton.IsEnabled = PrintButton.IsEnabled = hasSelection;
        MarkDeliveredButton.IsEnabled = ClearDeliveredButton.IsEnabled = hasSelection;
        if (hasSelection)
        {
            _lastAction = string.Empty;
        }
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

    private void SelectToPrint_Click(object sender, RoutedEventArgs e)
    {
        SetMonthSelection(cell => cell.NeedsPrinting);
        if (_selection.Count == 0)
        {
            _lastAction = "Nel mese non ci sono giorni confermati da stampare.";
            UpdateSummary();
        }
    }

    private void MarkDelivered_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var marked = _store.MarkDelivered(_selection);
            var skipped = _selection.Count - marked.Count;
            FinishDeliveryChange(DescribeMarked(marked.Count, skipped));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowDeliveryError(exception);
        }
    }

    private void ClearDelivered_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this,
                $"Togliere il contrassegno di consegnato a {FormatDays(_selection.Count)} selezionat{(_selection.Count == 1 ? "o" : "i")}?\n" +
                "Torneranno fra i giorni da stampare.",
                "Consegna", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            var cleared = _store.ClearDelivered(_selection);
            FinishDeliveryChange(cleared.Count == 0
                ? "Nessuno dei giorni selezionati era segnato come consegnato."
                : $"Contrassegno di consegnato tolto da {FormatDays(cleared.Count)}.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowDeliveryError(exception);
        }
    }

    private void FinishDeliveryChange(string message)
    {
        _selection.Clear();
        _lastAction = message;
        LoadMonth();
    }

    private void ShowDeliveryError(Exception exception) =>
        MessageBox.Show(this, $"Aggiornamento del contrassegno non riuscito: {exception.Message}", "Consegna",
            MessageBoxButton.OK, MessageBoxImage.Warning);

    private static string FormatDays(int count) => count == 1 ? "1 giorno" : $"{count} giorni";

    private static string DescribeMarked(int marked, int skippedDrafts) =>
        $"Segnati come consegnati: {FormatDays(marked)}." +
        (skippedDrafts > 0 ? $" Bozze non segnate (vanno prima confermate): {FormatDays(skippedDrafts)}." : string.Empty);

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
            return;
        }

        // Stampa inviata: i giorni confermati stampati risultano consegnati.
        try
        {
            var marked = _store.MarkDelivered(_selection);
            FinishDeliveryChange("Stampa inviata. " + DescribeMarked(marked.Count, _selection.Count - marked.Count));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowDeliveryError(exception);
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

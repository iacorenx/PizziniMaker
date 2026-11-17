using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public sealed class CalendarCellViewModel
{
    public DateOnly? Date { get; init; }
    public bool HasDate => Date.HasValue;
    public string DayNumber { get; init; } = string.Empty;
    public string Background { get; init; } = "#F7F5F0";
    public string NumberBrush { get; init; } = "#777168";
    public string BorderBrush { get; init; } = "#E8E2D7";
    public Thickness CellBorderThickness { get; init; } = new(1);
    public string Icons { get; init; } = string.Empty;
    public string HoursText { get; init; } = string.Empty;
    public string LockText { get; init; } = string.Empty;
    public string ToolTip { get; init; } = string.Empty;
}

public sealed class MonthlyCalendarViewModel : ObservableObject
{
    private readonly MainViewModel _mainViewModel;
    private DateOnly _displayedMonth;
    private string _monthTitle = string.Empty;
    private string _leaveSummary = string.Empty;
    private string _sickSummary = string.Empty;
    private string _overtimeSummary = string.Empty;
    private string _missingSummary = string.Empty;
    private int _missingDays;

    public MonthlyCalendarViewModel(MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        _displayedMonth = new DateOnly(mainViewModel.CurrentDate.Year, mainViewModel.CurrentDate.Month, 1);
        LoadMonth();
    }

    public ObservableCollection<CalendarCellViewModel> Cells { get; } = [];

    public string MonthTitle { get => _monthTitle; private set => SetProperty(ref _monthTitle, value); }
    public string LeaveSummary { get => _leaveSummary; private set => SetProperty(ref _leaveSummary, value); }
    public string SickSummary { get => _sickSummary; private set => SetProperty(ref _sickSummary, value); }
    public string OvertimeSummary { get => _overtimeSummary; private set => SetProperty(ref _overtimeSummary, value); }
    public string MissingSummary { get => _missingSummary; private set => SetProperty(ref _missingSummary, value); }
    public int MissingDays { get => _missingDays; private set => SetProperty(ref _missingDays, value); }

    public void PreviousMonth()
    {
        _displayedMonth = _displayedMonth.AddMonths(-1);
        LoadMonth();
    }

    public void NextMonth()
    {
        _displayedMonth = _displayedMonth.AddMonths(1);
        LoadMonth();
    }

    private void LoadMonth()
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        MonthTitle = culture.TextInfo.ToTitleCase(_displayedMonth.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", culture));
        var report = _mainViewModel.GetMonthlyReport(_displayedMonth.Year, _displayedMonth.Month);
        var reportDays = report.Days.ToDictionary(day => day.Date);
        var leadingCells = ((int)_displayedMonth.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;

        Cells.Clear();
        for (var index = 0; index < 42; index++)
        {
            var date = _displayedMonth.AddDays(index - leadingCells);
            if (index < leadingCells || date.Month != _displayedMonth.Month)
            {
                Cells.Add(new CalendarCellViewModel());
                continue;
            }

            Cells.Add(CreateCell(reportDays[date]));
        }

        LeaveSummary = $"🏝️ Ferie/permesso: {FormatDayCount(report.LeaveDays)} · {ActivityCatalog.FormatDuration(report.LeaveMinutes)}";
        SickSummary = $"✚ Malattia: {FormatDayCount(report.SickDays)} · {ActivityCatalog.FormatDuration(report.SickMinutes)}";
        OvertimeSummary = $"€€ Straordinario: {FormatDayCount(report.OvertimeDays)} · {ActivityCatalog.FormatDuration(report.OvertimeMinutes)}";
        MissingSummary = $"! Da completare: {FormatDayCount(report.MissingDays)} · {ActivityCatalog.FormatDuration(report.MissingMinutes)} mancanti";
        MissingDays = report.MissingDays;
    }

    private static string FormatDayCount(int count) => count == 1 ? "1 giorno" : $"{count} giorni";

    private static CalendarCellViewModel CreateCell(MonthlyDayInfo day)
    {
        var background = day.IsComplete ? "#DDF3DF"
            : day.IsIncomplete ? "#FCE4D6"
            : day.IsNationalHoliday || day.IsSunday ? "#F2F0EC"
            : day.HasSavedData && !day.IsConfirmed ? "#FFF8E7" : "#FFFFFF";
        var numberBrush = day.IsNationalHoliday ? "#C62828" : day.IsIncomplete ? "#A74716" : "#303C32";
        var borderBrush = day.IsToday ? "#2176C7" : day.IsIncomplete ? "#D98555" : "#E0DCD2";
        var tooltip = ActivityCatalog.FormatDate(day.Date);
        if (day.HolidayName is not null)
        {
            tooltip += $"\n{day.HolidayName}";
        }
        if (day.HasSavedData)
        {
            tooltip += $"\nTotale: {ActivityCatalog.FormatDuration(day.TotalMinutes)}";
        }
        if (day.IsIncomplete)
        {
            tooltip += "\nGiorno obbligatorio incompleto";
        }

        return new CalendarCellViewModel
        {
            Date = day.Date,
            DayNumber = day.Date.Day.ToString(),
            Background = background,
            NumberBrush = numberBrush,
            BorderBrush = borderBrush,
            CellBorderThickness = new Thickness(day.IsToday ? 2 : 1),
            Icons = string.Join(" ", day.Icons),
            HoursText = day.HasSavedData ? $"{ActivityCatalog.FormatHours(day.TotalMinutes)} h" : day.IsIncomplete ? "!" : string.Empty,
            LockText = day.IsLocked ? "🔒" : string.Empty,
            ToolTip = tooltip
        };
    }
}

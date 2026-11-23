using System.Collections.ObjectModel;
using System.IO;
using PizziniMaker.Core;

namespace PizziniMaker.App;

public sealed class MainViewModel : ObservableObject
{
    private readonly PizziniStore _store;
    private WorkDay _currentDay = null!;
    private string _persistenceStatus = string.Empty;
    private string _lastError = string.Empty;
    private string _weekStatusText = string.Empty;
    private string _weekBadgeBrush = "#E8F5E9";
    private string _weekTextBrush = "#256B3A";
    private bool _showWeekStatus;
    private int _missingDaysCount;

    private WorkCardHistory _history = new();

    public MainViewModel(PizziniStore store, AppSettings settings)
    {
        _store = store;
        Settings = settings;
        ReloadHistory();
        SetDay(store.LoadDay(DateOnly.FromDateTime(DateTime.Today)));
    }

    public WorkCardHistory History => _history;

    public PizziniStore Store => _store;

    private string _dayExpensesBadge = string.Empty;
    private string _expensesToolTip = "Note spese";

    /// <summary>Numero di spese del giorno mostrato accanto all'icona (vuoto se nessuna).</summary>
    public string DayExpensesBadge { get => _dayExpensesBadge; private set => SetProperty(ref _dayExpensesBadge, value); }

    public string ExpensesToolTip { get => _expensesToolTip; private set => SetProperty(ref _expensesToolTip, value); }

    public void RefreshExpenses()
    {
        try
        {
            var expenses = new ExpenseStore(_store).LoadDay(CurrentDate);
            DayExpensesBadge = expenses.Count > 0 ? expenses.Count.ToString() : string.Empty;
            ExpensesToolTip = expenses.Count == 0
                ? "Note spese (pasti, viaggi, chilometri)"
                : $"Note spese del giorno: {expenses.Count} · {ActivityCatalog.FormatEuro(expenses.Sum(entry => entry.TotalCents))}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            DayExpensesBadge = "!";
            ExpensesToolTip = $"Note spese non leggibili: {exception.Message}";
        }
    }

    public void ReloadHistory()
    {
        try
        {
            _history = _store.LoadOrRebuildHistory();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _history = new WorkCardHistory();
            LastError = $"Storico schede non disponibile: {exception.Message}";
        }
    }

    public bool IsTrasferta
    {
        get => _currentDay.IsTrasferta;
        set
        {
            if (_currentDay.IsTrasferta == value || !CanEditDay)
            {
                return;
            }

            _currentDay.IsTrasferta = value;
            OnPropertyChanged();
            SaveCurrentDay();
        }
    }

    public bool IsOptionalDay => DailyHours.IsOptionalDay(CurrentDate);

    public bool ShowOptionalDayHint => IsOptionalDay && CanEditDay;

    public ObservableCollection<ActivityEntryViewModel> Entries { get; } = [];

    public AppSettings Settings { get; private set; }

    public DateOnly CurrentDate { get; private set; }

    public string EmployeeName => Settings.EmployeeName;

    public string DateTitle => ActivityCatalog.FormatDate(CurrentDate);

    public bool IsConfirmed => _currentDay.IsConfirmed;

    public bool IsNotConfirmed => !IsConfirmed;

    public bool IsDayLocked => IsNotConfirmed && ItalianHolidayCalendar.IsNonWorkingDay(CurrentDate) &&
        !_currentDay.AllowNonWorkingDay;

    public bool CanEditDay => IsNotConfirmed && !IsDayLocked;

    public string NonWorkingDayMessage => CurrentDate.DayOfWeek == DayOfWeek.Sunday
        ? "Domenica: la compilazione è bloccata. Puoi forzare l'apertura solo per questo giorno."
        : $"{ItalianHolidayCalendar.GetHolidayName(CurrentDate)}: la compilazione della festività è bloccata.";

    public string ConfirmationStatusText => IsConfirmed
        ? $"✓ Giorno confermato il {_currentDay.ConfirmedAt?.ToLocalTime():dd/MM/yyyy HH:mm}"
        : "Bozza non confermata";

    public string TotalText => $"Totale: {ActivityCatalog.FormatDuration(_currentDay.TotalMinutes)}";

    public string HoursDetailText
    {
        get
        {
            var parts = new List<string>();
            var overtime = DailyHours.GetOvertimeMinutes(_currentDay);
            if (overtime > 0)
            {
                parts.Add($"Straordinario: {ActivityCatalog.FormatDuration(overtime)}");
            }
            var travel = DailyHours.GetTravelMinutes(_currentDay);
            if (travel > 0)
            {
                parts.Add($"Viaggio: {ActivityCatalog.FormatDuration(travel)}");
            }
            return string.Join(" · ", parts);
        }
    }

    public bool HasHoursDetail => HoursDetailText.Length > 0;

    public string LogDirectory => _store.LogDirectory;

    public string PersistenceStatus
    {
        get => _persistenceStatus;
        private set => SetProperty(ref _persistenceStatus, value);
    }

    public string LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public string WeekStatusText
    {
        get => _weekStatusText;
        private set => SetProperty(ref _weekStatusText, value);
    }

    public string WeekBadgeBrush
    {
        get => _weekBadgeBrush;
        private set => SetProperty(ref _weekBadgeBrush, value);
    }

    public string WeekTextBrush
    {
        get => _weekTextBrush;
        private set => SetProperty(ref _weekTextBrush, value);
    }

    public bool ShowWeekStatus
    {
        get => _showWeekStatus;
        private set => SetProperty(ref _showWeekStatus, value);
    }

    public int MissingDaysCount
    {
        get => _missingDaysCount;
        private set
        {
            if (SetProperty(ref _missingDaysCount, value))
            {
                OnPropertyChanged(nameof(HasMissingDays));
                OnPropertyChanged(nameof(MissingDaysText));
            }
        }
    }

    public bool HasMissingDays => MissingDaysCount > 0;

    public string MissingDaysText => MissingDaysCount == 1
        ? "! 1 giorno precedente da compilare"
        : $"! {MissingDaysCount} giorni precedenti da compilare";

    public bool ChangeDate(DateOnly date)
    {
        if (date == CurrentDate)
        {
            return true;
        }

        if (!SaveCurrentDay())
        {
            return false;
        }

        SetDay(_store.LoadDay(date));
        return true;
    }

    public void AddEntry()
    {
        if (!CanEditDay)
        {
            return;
        }

        var entry = new ActivityEntry();
        _currentDay.Entries.Add(entry);
        Entries.Add(new ActivityEntryViewModel(entry, Settings.CustomActivities, OnEntryChanged, () => _history, _currentDay.Date.Year));
        NotifyTotals();
        SaveCurrentDay();
    }

    public void RemoveEntry(ActivityEntryViewModel entry)
    {
        if (!CanEditDay)
        {
            return;
        }

        var model = _currentDay.Entries.Single(item => item.Id == entry.Id);
        _currentDay.Entries.Remove(model);
        Entries.Remove(entry);
        NotifyTotals();
        SaveCurrentDay();
    }

    public bool SaveCurrentDay()
    {
        try
        {
            _store.SaveDay(_currentDay);
            LastError = string.Empty;
            PersistenceStatus = IsConfirmed ? "Modifiche salvate." : $"Bozza salvata alle {DateTime.Now:HH:mm}.";
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Salvataggio non riuscito: {exception.Message}";
            PersistenceStatus = "Errore di salvataggio.";
            return false;
        }
    }

    private void OnEntryChanged(ActivityEntryViewModel entry, string? propertyName)
    {
        NotifyTotals();

        if (IsConfirmed && propertyName == nameof(ActivityEntryViewModel.IsPaperRecorded))
        {
            try
            {
                _store.UpdatePaperStatus(CurrentDate, entry.Id, entry.IsPaperRecorded, Settings.EmployeeName);
                LastError = string.Empty;
                PersistenceStatus = "Registrazione cartacea salvata e log aggiornato.";
            }
            catch (Exception exception)
            {
                LastError = $"Aggiornamento non riuscito: {exception.Message}";
                PersistenceStatus = "Errore di aggiornamento.";
            }
            return;
        }

        SaveCurrentDay();
    }

    public IReadOnlyList<string> ValidateEntries()
    {
        var errors = WorkDayValidator.Validate(_currentDay).ToList();
        if (IsDayLocked)
        {
            errors.Add("Abilitare questo giorno non lavorativo tramite la forzatura dedicata.");
        }
        return errors;
    }

    public DayClosureAssessment GetClosureAssessment() => DailyHours.Assess(_currentDay);

    public bool CompleteDayWith(ActivityType type)
    {
        if (type is not ActivityType.Qu and not ActivityType.FeriePermesso)
        {
            throw new ArgumentException("Il completamento è consentito solo con QU o FERIE/PERMESSO.", nameof(type));
        }

        var missingMinutes = DailyHours.Assess(_currentDay).MissingMinutes;
        if (missingMinutes == 0)
        {
            return true;
        }

        var entry = new ActivityEntry { Type = type, Minutes = missingMinutes, IsCad = false };
        _currentDay.Entries.Add(entry);
        Entries.Add(new ActivityEntryViewModel(entry, Settings.CustomActivities, OnEntryChanged, () => _history, _currentDay.Date.Year));
        NotifyTotals();
        return SaveCurrentDay();
    }

    public bool ForceNonWorkingDay()
    {
        if (!IsDayLocked)
        {
            return false;
        }

        _currentDay.AllowNonWorkingDay = true;
        NotifyDayProperties();
        foreach (var entry in Entries)
        {
            entry.SetLocked(false);
        }
        return SaveCurrentDay();
    }

    public MonthlyReport GetMonthlyReport(int year, int month) =>
        MonthlySummaryService.BuildMonth(year, month, DateOnly.FromDateTime(DateTime.Today),
            Settings.TrackingStartDate, _store.LoadExistingDay);

    public IReadOnlyList<string> ConfirmDay()
    {
        var errors = WorkDayValidator.ValidateForConfirmation(_currentDay);
        if (errors.Count > 0)
        {
            return errors;
        }

        if (!SaveCurrentDay())
        {
            return [LastError];
        }

        try
        {
            SetDay(_store.ConfirmDay(CurrentDate, Settings.EmployeeName));
            ReloadHistory();
            LastError = string.Empty;
            PersistenceStatus = "Giorno acquisito e log generato.";
            return [];
        }
        catch (Exception exception)
        {
            LastError = $"Conferma non riuscita: {exception.Message}";
            return [LastError];
        }
    }

    public bool ReopenDay()
    {
        try
        {
            SetDay(_store.ReopenDay(CurrentDate));
            LastError = string.Empty;
            PersistenceStatus = "Giorno riaperto. Il precedente log è stato rimosso.";
            return true;
        }
        catch (Exception exception)
        {
            LastError = $"Riapertura non riuscita: {exception.Message}";
            return false;
        }
    }

    public void ApplySettings(AppSettings settings)
    {
        Settings = settings;
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(EmployeeName));
        SetDay(_currentDay);
    }

    /// <summary>Ricarica giornata corrente e storico dal disco (dopo importazione o modifica dello storico).</summary>
    public void ReloadFromStore()
    {
        ReloadHistory();
        SetDay(_store.LoadDay(CurrentDate));
    }

    public IReadOnlyList<DateOnly> GetMissingDays() =>
        _store.GetMissingWorkdays(DateOnly.FromDateTime(DateTime.Today), Settings.TrackingStartDate);

    private void SetDay(WorkDay day)
    {
        _currentDay = day;
        CurrentDate = day.Date;
        Entries.Clear();
        foreach (var model in day.Entries)
        {
            var entry = new ActivityEntryViewModel(model, Settings.CustomActivities, OnEntryChanged, () => _history, _currentDay.Date.Year);
            entry.SetLocked(day.IsConfirmed || IsDayLocked);
            Entries.Add(entry);
        }

        NotifyDayProperties();
        RefreshCalendarStatus();
        RefreshExpenses();
    }

    private void NotifyTotals()
    {
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(HoursDetailText));
        OnPropertyChanged(nameof(HasHoursDetail));
    }

    private void NotifyDayProperties()
    {
        OnPropertyChanged(nameof(CurrentDate));
        OnPropertyChanged(nameof(DateTitle));
        OnPropertyChanged(nameof(IsConfirmed));
        OnPropertyChanged(nameof(IsNotConfirmed));
        OnPropertyChanged(nameof(IsDayLocked));
        OnPropertyChanged(nameof(CanEditDay));
        OnPropertyChanged(nameof(NonWorkingDayMessage));
        OnPropertyChanged(nameof(IsTrasferta));
        OnPropertyChanged(nameof(IsOptionalDay));
        OnPropertyChanged(nameof(ShowOptionalDayHint));
        OnPropertyChanged(nameof(ConfirmationStatusText));
        NotifyTotals();
        PersistenceStatus = IsConfirmed ? "Giorno acquisito." : "Bozza salvata automaticamente.";
    }

    private void RefreshCalendarStatus()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        MissingDaysCount = _store.GetMissingWorkdays(today, Settings.TrackingStartDate).Count;
        var status = WorkdayCalendar.EvaluateWeek(CurrentDate, today, Settings.TrackingStartDate, _store.IsComplete);

        switch (status.Kind)
        {
            case WeekStatusKind.UpToDate:
                WeekStatusText = "✓ Settimana in pari";
                WeekBadgeBrush = "#E5F4E8";
                WeekTextBrush = "#23713B";
                ShowWeekStatus = true;
                break;
            case WeekStatusKind.Complete:
                WeekStatusText = "✓ Settimana completa";
                WeekBadgeBrush = "#E5F4E8";
                WeekTextBrush = "#23713B";
                ShowWeekStatus = true;
                break;
            case WeekStatusKind.Incomplete:
                WeekStatusText = "! Settimana incompleta";
                WeekBadgeBrush = "#FFF0E5";
                WeekTextBrush = "#A74716";
                ShowWeekStatus = true;
                break;
            default:
                ShowWeekStatus = false;
                break;
        }
    }
}

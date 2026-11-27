using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PizziniMaker.Core;

public sealed class PizziniStore
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public PizziniStore(string rootDirectory, bool createDirectories = true)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("La cartella dati non può essere vuota.", nameof(rootDirectory));
        }

        RootDirectory = Path.GetFullPath(rootDirectory);
        DataDirectory = Path.Combine(RootDirectory, "Dati");
        LogDirectory = Path.Combine(RootDirectory, "Log");
        SettingsFilePath = Path.Combine(RootDirectory, "Impostazioni.json");
        HistoryFilePath = Path.Combine(RootDirectory, "StoricoSchede.json");
        BackupDirectory = Path.Combine(RootDirectory, "Backup");
        ExpenseDirectory = Path.Combine(RootDirectory, "NoteSpese");
        if (createDirectories)
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(LogDirectory);
        }
    }

    public string RootDirectory { get; }

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    public string SettingsFilePath { get; }

    public string HistoryFilePath { get; }

    public string BackupDirectory { get; }

    public string ExpenseDirectory { get; }

    internal T? LoadJsonFile<T>(string path) where T : class => LoadJson<T>(path);

    internal void WriteJsonFile<T>(string path, T value) => WriteJsonAtomic(path, value);

    public AppSettings LoadSettings(bool saveMigration = true)
    {
        var stored = LoadJson<AppSettings>(SettingsFilePath);
        var settings = stored ?? new AppSettings();
        settings.CustomActivities ??= AppSettings.CreateDefaultCustomActivities();
        var migrated = MigrateSettings(settings);
        if (saveMigration && stored is not null && migrated)
        {
            // La migrazione viene salvata subito, così l'identificativo del dipendente resta stabile.
            SaveSettings(settings);
        }
        return settings;
    }

    /// <summary>Porta le impostazioni allo schema corrente. Restituisce true se è cambiato qualcosa.</summary>
    public static bool MigrateSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var original = settings.SchemaVersion;
        if (original < 3)
        {
            // Dalla 1.2.0 la trasferta è una qualifica della giornata e non più un'attività con ore.
            settings.CustomActivities.RemoveAll(activity => string.Equals(activity.Name?.Trim(),
                AppSettings.LegacyTrasfertaActivityName, StringComparison.OrdinalIgnoreCase));
        }
        if (original < 4)
        {
            // Dalla 1.3.0 MANDATO e PREVENTIVO sono attività predefinite con numero di riferimento.
            settings.CustomActivities.RemoveAll(activity =>
                string.Equals(activity.Name?.Trim(), "MANDATO", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(activity.Name?.Trim(), "PREVENTIVO", StringComparison.OrdinalIgnoreCase));
        }
        if (settings.EmployeeId == Guid.Empty)
        {
            settings.EmployeeId = Guid.NewGuid();
        }
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        return original != settings.SchemaVersion;
    }

    public WorkCardHistory? LoadHistory() => LoadJson<WorkCardHistory>(HistoryFilePath) is { } history
        ? Normalize(history)
        : null;

    /// <summary>
    /// Carica lo storico schede; se manca o non è leggibile lo ricostruisce dalle giornate confermate.
    /// </summary>
    public WorkCardHistory LoadOrRebuildHistory()
    {
        try
        {
            if (LoadHistory() is { } history)
            {
                return history;
            }
        }
        catch (InvalidDataException)
        {
            // Lo storico è un dato derivato: se è danneggiato viene ricostruito dalle giornate.
        }

        var rebuilt = WorkCardHistory.Rebuild(LoadAllDays(out _));
        SaveHistory(rebuilt);
        return rebuilt;
    }

    public void SaveHistory(WorkCardHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        WriteJsonAtomic(HistoryFilePath, history);
    }

    private static WorkCardHistory Normalize(WorkCardHistory history)
    {
        history.Entries ??= [];
        history.Entries.RemoveAll(entry => !WorkCardHistory.IsValidEntry(entry));
        foreach (var entry in history.Entries)
        {
            entry.Comment = WorkCardHistory.NormalizeComment(entry.Comment);
        }
        return history;
    }

    /// <summary>Date delle giornate salvate (file <c>yyyy-MM-dd.json</c> o solo il relativo <c>.bak</c>).</summary>
    public IReadOnlyList<DateOnly> EnumerateDayDates()
    {
        if (!Directory.Exists(DataDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(DataDirectory)
            .Select(Path.GetFileName)
            .Select(name => name!.EndsWith(".json.bak", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name)
            .Select(name => TryParseDayFileName(name, out var date) ? date : (DateOnly?)null)
            .OfType<DateOnly>()
            .Distinct()
            .Order()
            .ToArray();
    }

    public static bool TryParseDayFileName(string? fileName, out DateOnly date)
    {
        date = default;
        return fileName is not null && fileName.Length == 15 &&
            fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
            DateOnly.TryParseExact(fileName[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    /// <summary>Carica tutte le giornate leggibili; le date non leggibili sono restituite in <paramref name="unreadable"/>.</summary>
    public IReadOnlyList<WorkDay> LoadAllDays(out IReadOnlyList<DateOnly> unreadable)
    {
        var days = new List<WorkDay>();
        var errors = new List<DateOnly>();
        foreach (var date in EnumerateDayDates())
        {
            try
            {
                if (LoadExistingDay(date) is { } day)
                {
                    days.Add(day);
                }
            }
            catch (InvalidDataException)
            {
                errors.Add(date);
            }
        }

        unreadable = errors;
        return days;
    }

    public void SaveSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        WriteJsonAtomic(SettingsFilePath, settings);
    }

    public WorkDay LoadDay(DateOnly date) => LoadExistingDay(date) ?? WorkDay.CreateNew(date);

    public WorkDay? LoadExistingDay(DateOnly date)
    {
        var path = GetDayFilePath(date);
        var isLegacyFile = IsLegacyWorkDayFile(path);
        var day = LoadJson<WorkDay>(path);
        if (isLegacyFile && day is not null && ItalianHolidayCalendar.IsNonWorkingDay(date) &&
            (day.IsConfirmed || day.Entries.Any(HasHistoricalContent)))
        {
            day.AllowNonWorkingDay = true;
        }
        return day;
    }

    public bool DayExists(DateOnly date) => File.Exists(GetDayFilePath(date));

    private static bool IsLegacyWorkDayFile(string path)
    {
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return !File.ReadAllText(candidate).Contains("\"allowNonWorkingDay\"", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
        return false;
    }

    private static bool HasHistoricalContent(ActivityEntry entry) =>
        entry.Type != ActivityType.Scheda || entry.Minutes != 30 || !string.IsNullOrEmpty(entry.WorkCardNumber) ||
        !string.IsNullOrEmpty(entry.WorkCardComment) ||
        !entry.IsCad || entry.IsPaperRecorded || !string.IsNullOrEmpty(entry.CustomActivityName);

    public void SaveDay(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        day.LastModifiedAt = DateTimeOffset.Now;
        day.Revision++;
        WriteJsonAtomic(GetDayFilePath(day.Date), day);
    }

    public WorkDay ConfirmDay(DateOnly date, string employeeName)
    {
        var day = LoadDay(date);
        var errors = WorkDayValidator.ValidateForConfirmation(day);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        day.IsConfirmed = true;
        day.ConfirmedAt = DateTimeOffset.Now;
        SaveDay(day);
        WriteLog(day, employeeName);
        RecordInHistory(day);
        return day;
    }

    private void RecordInHistory(WorkDay day)
    {
        try
        {
            var history = LoadOrRebuildHistory();
            history.RecordDay(day);
            SaveHistory(history);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Lo storico serve solo ai suggerimenti: un errore non deve impedire la conferma della giornata.
        }
    }

    /// <summary>Scrive una giornata importata così com'è, senza alterarne la data di ultima modifica.</summary>
    public void WriteImportedDay(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        WriteJsonAtomic(GetDayFilePath(day.Date), day);
    }

    /// <summary>
    /// Segna come consegnati (stampati e dati all'addetta) i giorni confermati con attività; le bozze sono ignorate.
    /// Non è una modifica del contenuto: data di ultima modifica e revisione restano invariate.
    /// Restituisce le date effettivamente segnate.
    /// </summary>
    public IReadOnlyList<DateOnly> MarkDelivered(IEnumerable<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var marked = new List<DateOnly>();
        var now = DateTimeOffset.Now;
        foreach (var date in dates.Distinct().Order())
        {
            if (LoadExistingDay(date) is not { IsConfirmed: true } day || !PrintDelivery.HasPrintableContent(day))
            {
                continue;
            }
            day.DeliveredAt = now;
            day.DeliveredFingerprint = PrintSlipBuilder.GetFingerprint(day);
            WriteJsonAtomic(GetDayFilePath(date), day);
            marked.Add(date);
        }
        return marked;
    }

    /// <summary>Toglie il contrassegno di consegna; restituisce le date che lo avevano.</summary>
    public IReadOnlyList<DateOnly> ClearDelivered(IEnumerable<DateOnly> dates)
    {
        ArgumentNullException.ThrowIfNull(dates);
        var cleared = new List<DateOnly>();
        foreach (var date in dates.Distinct().Order())
        {
            if (LoadExistingDay(date) is not { } day || (day.DeliveredAt is null && day.DeliveredFingerprint is null))
            {
                continue;
            }
            day.DeliveredAt = null;
            day.DeliveredFingerprint = null;
            WriteJsonAtomic(GetDayFilePath(date), day);
            cleared.Add(date);
        }
        return cleared;
    }

    /// <summary>Giorni confermati ancora da stampare (mai consegnati o modificati dopo la consegna).</summary>
    public IReadOnlyList<DateOnly> GetDaysToPrint() =>
        LoadAllDays(out _).Where(PrintDelivery.NeedsPrinting).Select(day => day.Date).ToArray();

    public WorkDay ReopenDay(DateOnly date)
    {
        var day = LoadDay(date);
        day.IsConfirmed = false;
        day.ConfirmedAt = null;
        SaveDay(day);

        var logPath = GetLogFilePath(date);
        File.Delete(logPath);
        File.Delete(logPath + ".bak");
        return day;
    }

    public WorkDay UpdatePaperStatus(DateOnly date, Guid entryId, bool isPaperRecorded, string employeeName)
    {
        var day = LoadDay(date);
        var entry = day.Entries.SingleOrDefault(item => item.Id == entryId)
            ?? throw new InvalidOperationException("La riga di attività non è stata trovata.");

        entry.IsPaperRecorded = isPaperRecorded;
        SaveDay(day);
        if (day.IsConfirmed)
        {
            WriteLog(day, employeeName);
        }

        return day;
    }

    public IReadOnlyList<DateOnly> GetMissingWorkdays(DateOnly today, DateOnly trackingStartDate)
    {
        var lastDate = today.AddDays(-1);
        if (trackingStartDate > lastDate)
        {
            return [];
        }

        return WorkdayCalendar.EnumerateWorkdays(trackingStartDate, lastDate)
            .Where(date => !DailyHours.IsComplete(LoadExistingDay(date)))
            .ToArray();
    }

    public bool IsComplete(DateOnly date) => DailyHours.IsComplete(LoadExistingDay(date));

    public string GetLogFilePath(DateOnly date) =>
        Path.Combine(LogDirectory, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".txt");

    public string GetDayFilePath(DateOnly date) =>
        Path.Combine(DataDirectory, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json");

    private T? LoadJson<T>(string path) where T : class
    {
        Exception? mainFileError = null;
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(candidate, Utf8WithoutBom);
                return JsonSerializer.Deserialize<T>(json, _jsonOptions)
                    ?? throw new JsonException("Il file non contiene dati.");
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                if (candidate == path)
                {
                    mainFileError = exception;
                    continue;
                }

                throw new InvalidDataException($"Impossibile leggere il file e il suo backup: {path}", exception);
            }
        }

        if (mainFileError is not null)
        {
            throw new InvalidDataException($"Impossibile leggere il file: {path}", mainFileError);
        }

        return null;
    }

    private void WriteJsonAtomic<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, _jsonOptions);
        WriteTextAtomic(path, json);
    }

    private void WriteLog(WorkDay day, string employeeName) =>
        WriteTextAtomic(GetLogFilePath(day.Date), LogFormatter.Format(day, employeeName));

    private static void WriteTextAtomic(string path, string contents)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Percorso di destinazione non valido.");
        Directory.CreateDirectory(directory);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(temporaryPath, contents, Utf8WithoutBom);
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, path + ".bak", true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

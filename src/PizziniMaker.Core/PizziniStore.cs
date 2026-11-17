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

    public PizziniStore(string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
        {
            throw new ArgumentException("La cartella dati non può essere vuota.", nameof(rootDirectory));
        }

        RootDirectory = Path.GetFullPath(rootDirectory);
        DataDirectory = Path.Combine(RootDirectory, "Dati");
        LogDirectory = Path.Combine(RootDirectory, "Log");
        SettingsFilePath = Path.Combine(RootDirectory, "Impostazioni.json");
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    public string RootDirectory { get; }

    public string DataDirectory { get; }

    public string LogDirectory { get; }

    public string SettingsFilePath { get; }

    public AppSettings LoadSettings()
    {
        var settings = LoadJson<AppSettings>(SettingsFilePath) ?? new AppSettings();
        settings.SchemaVersion = 2;
        settings.CustomActivities ??= AppSettings.CreateDefaultCustomActivities();
        return settings;
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
        !entry.IsCad || entry.IsPaperRecorded || !string.IsNullOrEmpty(entry.CustomActivityName);

    public void SaveDay(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        day.LastModifiedAt = DateTimeOffset.Now;
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
        return day;
    }

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

    private string GetDayFilePath(DateOnly date) =>
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

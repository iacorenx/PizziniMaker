using System.Globalization;

namespace PizziniMaker.Core;

public sealed record ImportIssue(string Item, string Reason);

public sealed class ImportPreview
{
    public required string SourceRoot { get; init; }

    /// <summary>Giornate assenti nella cartella attuale: verranno importate.</summary>
    public required IReadOnlyList<WorkDay> NewDays { get; init; }

    /// <summary>Giornate presenti in entrambe le cartelle: viene mantenuta quella attuale.</summary>
    public required IReadOnlyList<DateOnly> ConflictingDays { get; init; }

    /// <summary>Log da copiare (solo per date senza giornata e senza log nella cartella attuale).</summary>
    public required IReadOnlyList<DateOnly> LogsToImport { get; init; }

    public required IReadOnlyList<ImportIssue> InvalidItems { get; init; }

    public required WorkCardHistory SourceHistory { get; init; }

    public required int NewHistoryEntries { get; init; }

    public AppSettings? SourceSettings { get; init; }

    public required IReadOnlyList<CustomActivityDefinition> NewCustomActivities { get; init; }

    /// <summary>Note spese assenti nella cartella attuale (confronto per identificativo).</summary>
    public IReadOnlyList<ExpenseEntry> NewExpenses { get; init; } = [];

    public bool HasImportableData =>
        NewDays.Count > 0 || LogsToImport.Count > 0 || NewHistoryEntries > 0 || NewExpenses.Count > 0;
}

/// <param name="ImportSettings">Attività personalizzate, nome se mancante, data di inizio controllo più vecchia.</param>
/// <param name="ImportPreferences">
/// Nuova installazione: anche identificativo del dipendente, sempre in primo piano, avvio automatico,
/// tariffa km e controllo aggiornamenti vengono presi dalla versione precedente.
/// </param>
public sealed record ImportOptions(bool ImportSettings = false, bool ImportPreferences = false);

public sealed class ImportReport
{
    public required string BackupPath { get; init; }

    public List<DateOnly> ImportedDays { get; } = [];

    public List<DateOnly> KeptDays { get; } = [];

    public int ImportedLogs { get; set; }

    public int HistoryEntriesAdded { get; set; }

    public int CustomActivitiesAdded { get; set; }

    public bool SettingsUpdated { get; set; }

    public int ExpensesImported { get; set; }

    public List<ImportIssue> SkippedItems { get; } = [];
}

/// <summary>
/// Importazione non distruttiva dei dati di una versione precedente (vecchia cartella portabile).
/// La cartella di origine viene solo letta; nella cartella attuale non viene sovrascritto nulla.
/// </summary>
public static class LegacyImportService
{
    public static ImportPreview Analyze(string sourceRoot, PizziniStore target, AppSettings? currentSettings = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        var root = ResolveSourceRoot(sourceRoot, target);
        var source = new PizziniStore(root, createDirectories: false);
        var issues = new List<ImportIssue>();
        var newDays = new List<WorkDay>();
        var conflicts = new List<DateOnly>();

        foreach (var date in source.EnumerateDayDates())
        {
            WorkDay? day;
            try
            {
                day = source.LoadExistingDay(date);
            }
            catch (InvalidDataException)
            {
                issues.Add(new ImportIssue(FormatDayFile(date), "file non leggibile (anche il backup .bak è danneggiato o assente)"));
                continue;
            }

            if (day is null)
            {
                continue;
            }
            if (day.Date != date)
            {
                issues.Add(new ImportIssue(FormatDayFile(date), "la data contenuta non corrisponde al nome del file"));
                continue;
            }

            if (target.DayExists(date))
            {
                conflicts.Add(date);
            }
            else
            {
                Normalize(day);
                newDays.Add(day);
            }
        }

        issues.AddRange(FindUnrecognizedFiles(source.DataDirectory, ".json", "Dati"));

        var logs = new List<DateOnly>();
        if (Directory.Exists(source.LogDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(source.LogDirectory, "*.txt"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    issues.Add(new ImportIssue(Path.Combine("Log", Path.GetFileName(file)), "nome del file non riconosciuto"));
                    continue;
                }

                // Il log è sempre derivato dalla giornata: lo si importa solo se la giornata attuale non esiste.
                if (!target.DayExists(date) && !File.Exists(target.GetLogFilePath(date)))
                {
                    logs.Add(date);
                }
            }
        }

        var sourceHistory = WorkCardHistory.Rebuild(newDays);
        try
        {
            if (source.LoadHistory() is { } storedHistory)
            {
                sourceHistory.Merge(storedHistory);
            }
        }
        catch (InvalidDataException)
        {
            issues.Add(new ImportIssue("StoricoSchede.json", "file non leggibile: lo storico verrà ricostruito dalle giornate"));
        }

        var targetHistory = SafeLoadHistory(target);
        var newHistoryEntries = sourceHistory.Entries.Count(entry => targetHistory.Find(entry.Type, entry.Number) is null);

        AppSettings? sourceSettings = null;
        if (File.Exists(source.SettingsFilePath) || File.Exists(source.SettingsFilePath + ".bak"))
        {
            try
            {
                sourceSettings = source.LoadSettings(saveMigration: false);
            }
            catch (InvalidDataException)
            {
                issues.Add(new ImportIssue("Impostazioni.json", "file non leggibile"));
            }
        }

        return new ImportPreview
        {
            SourceRoot = root,
            NewDays = newDays,
            ConflictingDays = conflicts,
            LogsToImport = logs.Order().ToArray(),
            InvalidItems = issues,
            SourceHistory = sourceHistory,
            NewHistoryEntries = newHistoryEntries,
            SourceSettings = sourceSettings,
            NewCustomActivities = GetNewCustomActivities(sourceSettings, currentSettings),
            NewExpenses = FindNewExpenses(source, target, issues)
        };
    }

    private static IReadOnlyList<ExpenseEntry> FindNewExpenses(PizziniStore source, PizziniStore target, List<ImportIssue> issues)
    {
        var sourceExpenses = new ExpenseStore(source);
        var targetExpenses = new ExpenseStore(target);
        var result = new List<ExpenseEntry>();
        foreach (var (year, month) in sourceExpenses.EnumerateMonths())
        {
            try
            {
                var existing = targetExpenses.LoadMonth(year, month).Select(entry => entry.Id).ToHashSet();
                result.AddRange(sourceExpenses.LoadMonth(year, month).Where(entry => !existing.Contains(entry.Id)));
            }
            catch (InvalidDataException)
            {
                issues.Add(new ImportIssue(Path.Combine("NoteSpese", $"{year:0000}-{month:00}.json"), "file non leggibile"));
            }
        }
        return result;
    }

    public static ImportReport Execute(ImportPreview preview, PizziniStore target, AppSettings currentSettings, ImportOptions options)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(currentSettings);
        ArgumentNullException.ThrowIfNull(options);
        var root = ResolveSourceRoot(preview.SourceRoot, target);
        var source = new PizziniStore(root, createDirectories: false);

        var report = new ImportReport { BackupPath = DataBackup.Create(target, "Pre-import") };
        report.KeptDays.AddRange(preview.ConflictingDays);
        report.SkippedItems.AddRange(preview.InvalidItems);

        foreach (var day in preview.NewDays)
        {
            if (target.DayExists(day.Date))
            {
                report.KeptDays.Add(day.Date);
                continue;
            }

            target.WriteImportedDay(day);
            report.ImportedDays.Add(day.Date);
        }

        foreach (var date in preview.LogsToImport)
        {
            var destination = target.GetLogFilePath(date);
            if (File.Exists(destination) || (target.DayExists(date) && !report.ImportedDays.Contains(date)))
            {
                continue;
            }

            Directory.CreateDirectory(target.LogDirectory);
            File.Copy(source.GetLogFilePath(date), destination, overwrite: false);
            report.ImportedLogs++;
        }

        var history = SafeLoadHistory(target);
        var before = history.Entries.Count;
        history.Merge(preview.SourceHistory);
        target.SaveHistory(history);
        report.HistoryEntriesAdded = history.Entries.Count - before;

        var sourceExpenses = new ExpenseStore(source);
        report.ExpensesImported = new ExpenseStore(target).ImportEntries(preview.NewExpenses,
            entry => sourceExpenses.GetAttachmentPath(entry));

        if (options.ImportSettings && preview.SourceSettings is { } sourceSettings)
        {
            if (options.ImportPreferences)
            {
                // Nuova installazione: la nuova cartella prende identità e preferenze della precedente.
                currentSettings.EmployeeId = sourceSettings.EmployeeId;
                currentSettings.AlwaysOnTop = sourceSettings.AlwaysOnTop;
                currentSettings.LaunchAtStartup = sourceSettings.LaunchAtStartup;
                currentSettings.CheckUpdatesAtStartup = sourceSettings.CheckUpdatesAtStartup;
                if (currentSettings.KmRateCents <= 0)
                {
                    currentSettings.KmRateCents = sourceSettings.KmRateCents;
                }
                report.SettingsUpdated = true;
            }

            foreach (var activity in GetNewCustomActivities(sourceSettings, currentSettings))
            {
                currentSettings.CustomActivities.Add(activity);
                report.CustomActivitiesAdded++;
            }

            if (string.IsNullOrWhiteSpace(currentSettings.EmployeeName) && !string.IsNullOrWhiteSpace(sourceSettings.EmployeeName))
            {
                currentSettings.EmployeeName = sourceSettings.EmployeeName.Trim();
                report.SettingsUpdated = true;
            }

            if (sourceSettings.TrackingStartDate < currentSettings.TrackingStartDate)
            {
                currentSettings.TrackingStartDate = sourceSettings.TrackingStartDate;
                report.SettingsUpdated = true;
            }

            if (report.CustomActivitiesAdded > 0 || report.SettingsUpdated)
            {
                target.SaveSettings(currentSettings);
            }
        }

        report.ImportedDays.Sort();
        report.KeptDays.Sort();
        return report;
    }

    /// <summary>Crea un archivio ZIP con i dati attuali prima dell'importazione.</summary>
    public static string CreateBackup(PizziniStore target) => DataBackup.Create(target, "Pre-import");

    /// <summary>
    /// Verifica la cartella scelta. Accetta anche la sottocartella <c>Dati</c> e risale alla cartella principale.
    /// </summary>
    public static string ResolveSourceRoot(string sourceRoot, PizziniStore target)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot))
        {
            throw new InvalidOperationException("Selezionare la cartella della versione precedente.");
        }

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
        if (!Directory.Exists(root))
        {
            throw new InvalidOperationException("La cartella selezionata non esiste.");
        }

        if (string.Equals(Path.GetFileName(root), "Dati", StringComparison.OrdinalIgnoreCase) && !LooksLikeDataRoot(root) &&
            Path.GetDirectoryName(root) is { } parent && LooksLikeDataRoot(parent))
        {
            root = parent;
        }

        var current = Path.TrimEndingDirectorySeparator(target.RootDirectory);
        if (string.Equals(root, current, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La cartella selezionata è quella del programma in esecuzione. Scegliere la cartella della versione precedente.");
        }
        if (IsInside(root, current) || IsInside(current, root))
        {
            throw new InvalidOperationException("La cartella selezionata contiene, o è contenuta in, quella del programma in esecuzione.");
        }

        if (!LooksLikeDataRoot(root))
        {
            throw new InvalidOperationException("La cartella selezionata non contiene dati di PizziniMaker (cartella Dati o file Impostazioni.json).");
        }

        return root;
    }

    private static bool LooksLikeDataRoot(string path) =>
        Directory.Exists(Path.Combine(path, "Dati")) || File.Exists(Path.Combine(path, "Impostazioni.json"));

    private static bool IsInside(string path, string container) =>
        path.StartsWith(container + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static WorkCardHistory SafeLoadHistory(PizziniStore store)
    {
        try
        {
            return store.LoadOrRebuildHistory();
        }
        catch (InvalidDataException)
        {
            return new WorkCardHistory();
        }
    }

    private static IReadOnlyList<CustomActivityDefinition> GetNewCustomActivities(AppSettings? source, AppSettings? current)
    {
        if (source?.CustomActivities is null)
        {
            return [];
        }

        var existing = new HashSet<string>(
            (current?.CustomActivities ?? []).Select(activity => activity.Name.Trim()), StringComparer.OrdinalIgnoreCase);
        return source.CustomActivities
            .Where(activity => !string.IsNullOrWhiteSpace(activity.Name))
            .Where(activity => !ActivityCatalog.ReservedActivityNames.Contains(activity.Name.Trim().ToUpperInvariant()))
            .Where(activity => existing.Add(activity.Name.Trim()))
            .Select(activity => new CustomActivityDefinition
            {
                Id = Guid.NewGuid(),
                Name = activity.Name.Trim().ToUpperInvariant(),
                Icon = string.IsNullOrWhiteSpace(activity.Icon) ? "📌" : activity.Icon
            })
            .ToArray();
    }

    private static void Normalize(WorkDay day)
    {
        day.Entries ??= [];
        foreach (var entry in day.Entries)
        {
            entry.WorkCardNumber ??= string.Empty;
            entry.WorkCardComment ??= string.Empty;
            entry.CustomActivityName ??= string.Empty;
            entry.CustomActivityIcon ??= string.Empty;
        }
    }

    private static IEnumerable<ImportIssue> FindUnrecognizedFiles(string directory, string extension, string label)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var candidate = name.EndsWith(extension + ".bak", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
            if (!PizziniStore.TryParseDayFileName(candidate, out _))
            {
                yield return new ImportIssue(Path.Combine(label, name), "nome del file non riconosciuto");
            }
        }
    }

    private static string FormatDayFile(DateOnly date) =>
        Path.Combine("Dati", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".json");
}

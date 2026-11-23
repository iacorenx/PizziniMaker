using System.Globalization;
using System.Text.Json.Serialization;

namespace PizziniMaker.Core;

/// <summary>Categoria della spesa. Serializzata come numero: nuovi valori solo in coda.</summary>
public enum ExpenseCategory
{
    Pasto,
    Viaggio,
    Chilometri,
    Alloggio,
    Altro
}

/// <summary>Stato del rimborso. Serializzato come numero: nuovi valori solo in coda.</summary>
public enum ExpenseStatus
{
    DaRimborsare,
    Rimborsata
}

public sealed class ExpenseEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly Date { get; set; }

    public ExpenseCategory Category { get; set; } = ExpenseCategory.Pasto;

    public string Description { get; set; } = string.Empty;

    /// <summary>Importo in centesimi; per i chilometri viene calcolato da km e tariffa.</summary>
    public long AmountCents { get; set; }

    public decimal Km { get; set; }

    /// <summary>Tariffa €/km in centesimi al momento dell'inserimento (le modifiche successive non alterano le spese).</summary>
    public int KmRateCents { get; set; }

    /// <summary>Percorso dell'allegato relativo alla cartella <c>NoteSpese</c>; vuoto se assente.</summary>
    public string AttachmentFile { get; set; } = string.Empty;

    public ExpenseStatus Status { get; set; } = ExpenseStatus.DaRimborsare;

    public DateOnly? ReimbursedOn { get; set; }

    public DateTimeOffset LastModifiedAt { get; set; }

    [JsonIgnore]
    public bool HasAttachment => !string.IsNullOrWhiteSpace(AttachmentFile);

    [JsonIgnore]
    public long TotalCents => Category == ExpenseCategory.Chilometri
        ? ExpenseCalculator.GetKmAmountCents(Km, KmRateCents)
        : AmountCents;

    public ExpenseEntry Clone() => (ExpenseEntry)MemberwiseClone();
}

public sealed class ExpenseMonth
{
    public int SchemaVersion { get; set; } = 1;

    public List<ExpenseEntry> Entries { get; set; } = [];
}

public sealed record ExpenseSummary(
    int Count,
    long TotalCents,
    long ToReimburseCents,
    long ReimbursedCents,
    decimal Km,
    IReadOnlyDictionary<ExpenseCategory, long> ByCategory);

public static class ExpenseCalculator
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public static long GetKmAmountCents(decimal km, int rateCents) =>
        km <= 0 || rateCents <= 0 ? 0 : (long)Math.Round(km * rateCents, MidpointRounding.AwayFromZero);

    public static ExpenseSummary Summarize(IEnumerable<ExpenseEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToArray();
        return new ExpenseSummary(
            list.Length,
            list.Sum(entry => entry.TotalCents),
            list.Where(entry => entry.Status == ExpenseStatus.DaRimborsare).Sum(entry => entry.TotalCents),
            list.Where(entry => entry.Status == ExpenseStatus.Rimborsata).Sum(entry => entry.TotalCents),
            list.Where(entry => entry.Category == ExpenseCategory.Chilometri).Sum(entry => entry.Km),
            list.GroupBy(entry => entry.Category).ToDictionary(group => group.Key, group => group.Sum(entry => entry.TotalCents)));
    }

    public static IReadOnlyList<string> Validate(ExpenseEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var errors = new List<string>();
        if (entry.Category == ExpenseCategory.Chilometri)
        {
            if (entry.Km <= 0)
            {
                errors.Add("Indicare i chilometri percorsi.");
            }
            if (entry.KmRateCents <= 0)
            {
                errors.Add("Impostare la tariffa €/km nelle impostazioni.");
            }
        }
        else if (entry.AmountCents <= 0)
        {
            errors.Add("Indicare un importo maggiore di zero.");
        }
        if (entry.Category is ExpenseCategory.Altro && string.IsNullOrWhiteSpace(entry.Description))
        {
            errors.Add("Per la categoria Altro serve una descrizione.");
        }
        return errors;
    }

    /// <summary>Interpreta un importo scritto in italiano ("12,50", "12.50", "1.234,5"). Restituisce i centesimi.</summary>
    public static bool TryParseEuro(string? text, out long cents)
    {
        cents = 0;
        var value = (text ?? string.Empty).Replace("€", string.Empty).Trim();
        if (value.Length == 0)
        {
            return false;
        }
        if (value.Contains(','))
        {
            value = value.Replace(".", string.Empty);
        }
        else if (value.Count(character => character == '.') == 1 && value.Length - value.IndexOf('.') - 1 <= 2)
        {
            value = value.Replace('.', ',');
        }
        else
        {
            value = value.Replace(".", string.Empty);
        }
        if (!decimal.TryParse(value, NumberStyles.Number, ItalianCulture, out var amount) || amount < 0)
        {
            return false;
        }
        cents = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool TryParseKm(string? text, out decimal km)
    {
        var value = (text ?? string.Empty).Trim().Replace('.', ',');
        return decimal.TryParse(value, NumberStyles.Number, ItalianCulture, out km) && km >= 0;
    }

    public static string FormatKm(decimal km) => km.ToString("0.#", ItalianCulture);

    public static string ToDisplayName(this ExpenseCategory category) => category switch
    {
        ExpenseCategory.Pasto => "Pasto",
        ExpenseCategory.Viaggio => "Viaggio (biglietti, pedaggi, parcheggi)",
        ExpenseCategory.Chilometri => "Chilometri auto",
        ExpenseCategory.Alloggio => "Alloggio",
        ExpenseCategory.Altro => "Altro",
        _ => category.ToString()
    };

    public static string GetIcon(this ExpenseCategory category) => category switch
    {
        ExpenseCategory.Pasto => "🍴",
        ExpenseCategory.Viaggio => "🎫",
        ExpenseCategory.Chilometri => ActivityCatalog.TravelIcon,
        ExpenseCategory.Alloggio => "🛏",
        _ => "🧾"
    };
}

/// <summary>
/// Note spese: un file per mese in <c>NoteSpese/yyyy-MM.json</c>, allegati copiati in
/// <c>NoteSpese/Allegati/yyyy-MM/</c>. Le spese sono indipendenti dalla conferma della giornata.
/// </summary>
public sealed class ExpenseStore
{
    public const string AttachmentsFolderName = "Allegati";
    private readonly PizziniStore _store;

    public ExpenseStore(PizziniStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public string ExpenseDirectory => _store.ExpenseDirectory;

    public string GetMonthFilePath(int year, int month) =>
        Path.Combine(ExpenseDirectory, $"{year:0000}-{month:00}.json");

    public IReadOnlyList<ExpenseEntry> LoadMonth(int year, int month) =>
        (_store.LoadJsonFile<ExpenseMonth>(GetMonthFilePath(year, month))?.Entries ?? [])
            .Where(entry => entry is not null)
            .Select(Normalize)
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.LastModifiedAt)
            .ToArray();

    public IReadOnlyList<ExpenseEntry> LoadDay(DateOnly date) =>
        LoadMonth(date.Year, date.Month).Where(entry => entry.Date == date).ToArray();

    /// <summary>Mesi con note spese salvate, in ordine.</summary>
    public IReadOnlyList<(int Year, int Month)> EnumerateMonths()
    {
        if (!Directory.Exists(ExpenseDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(ExpenseDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => DateOnly.TryParseExact(name + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? (date.Year, date.Month) : ((int, int)?)null)
            .OfType<(int Year, int Month)>()
            .Order()
            .ToArray();
    }

    /// <summary>Aggiunge o aggiorna una spesa; se cambia mese viene spostata nel file corretto.</summary>
    public void Save(ExpenseEntry entry, DateOnly? previousDate = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var errors = ExpenseCalculator.Validate(entry);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        if (previousDate is { } previous && (previous.Year != entry.Date.Year || previous.Month != entry.Date.Month))
        {
            RemoveFromMonth(previous.Year, previous.Month, entry.Id);
        }

        entry.Description = (entry.Description ?? string.Empty).Trim();
        entry.LastModifiedAt = DateTimeOffset.Now;
        if (entry.Status == ExpenseStatus.DaRimborsare)
        {
            entry.ReimbursedOn = null;
        }

        var month = LoadMonthFile(entry.Date.Year, entry.Date.Month);
        month.Entries.RemoveAll(item => item.Id == entry.Id);
        month.Entries.Add(entry.Clone());
        _store.WriteJsonFile(GetMonthFilePath(entry.Date.Year, entry.Date.Month), month);
    }

    /// <summary>Elimina la spesa e la copia del suo allegato.</summary>
    public void Delete(ExpenseEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        RemoveFromMonth(entry.Date.Year, entry.Date.Month, entry.Id);
        DeleteAttachmentFile(entry.AttachmentFile);
    }

    public void SetStatus(ExpenseEntry entry, ExpenseStatus status, DateOnly? date = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entry.Status = status;
        entry.ReimbursedOn = status == ExpenseStatus.Rimborsata ? date ?? DateOnly.FromDateTime(DateTime.Today) : null;
        Save(entry);
    }

    /// <summary>
    /// Copia il file indicato tra gli allegati e lo associa alla spesa (l'originale non viene toccato).
    /// Restituisce il percorso relativo salvato nella spesa.
    /// </summary>
    public string CopyAttachment(ExpenseEntry entry, string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Il file da allegare non esiste.", sourcePath);
        }

        var folder = Path.Combine(AttachmentsFolderName, entry.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture));
        var name = $"{entry.Id:N}_{SanitizeFileName(Path.GetFileName(sourcePath))}";
        var relative = Path.Combine(folder, name);
        var destination = Path.Combine(ExpenseDirectory, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(sourcePath, destination, overwrite: true);
        if (!string.Equals(entry.AttachmentFile, relative, StringComparison.OrdinalIgnoreCase))
        {
            DeleteAttachmentFile(entry.AttachmentFile);
        }
        entry.AttachmentFile = relative;
        return relative;
    }

    public void RemoveAttachment(ExpenseEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        DeleteAttachmentFile(entry.AttachmentFile);
        entry.AttachmentFile = string.Empty;
    }

    public string? GetAttachmentPath(ExpenseEntry entry)
    {
        if (entry is null || !entry.HasAttachment)
        {
            return null;
        }
        var path = Path.GetFullPath(Path.Combine(ExpenseDirectory, entry.AttachmentFile));
        return IsInsideExpenseDirectory(path) && File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Aggiunge spese importate così come sono (mai sovrascritte se l'identificativo esiste già),
    /// copiando l'allegato quando disponibile. Restituisce il numero di spese aggiunte.
    /// </summary>
    public int ImportEntries(IEnumerable<ExpenseEntry> entries, Func<ExpenseEntry, string?> sourceAttachment)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var added = 0;
        foreach (var group in entries.GroupBy(entry => (entry.Date.Year, entry.Date.Month)))
        {
            var file = LoadMonthFile(group.Key.Year, group.Key.Month);
            var existing = file.Entries.Select(entry => entry.Id).ToHashSet();
            foreach (var entry in group.Where(entry => existing.Add(entry.Id)))
            {
                var copy = entry.Clone();
                if (copy.HasAttachment && sourceAttachment(entry) is { } attachmentPath)
                {
                    var destination = Path.GetFullPath(Path.Combine(ExpenseDirectory, copy.AttachmentFile));
                    if (IsInsideExpenseDirectory(destination) && !File.Exists(destination))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(attachmentPath, destination);
                    }
                }
                file.Entries.Add(copy);
                added++;
            }
            _store.WriteJsonFile(GetMonthFilePath(group.Key.Year, group.Key.Month), file);
        }
        return added;
    }

    private ExpenseMonth LoadMonthFile(int year, int month)
    {
        var file = _store.LoadJsonFile<ExpenseMonth>(GetMonthFilePath(year, month)) ?? new ExpenseMonth();
        file.Entries ??= [];
        file.Entries.RemoveAll(entry => entry is null);
        return file;
    }

    private void RemoveFromMonth(int year, int month, Guid id)
    {
        var file = LoadMonthFile(year, month);
        if (file.Entries.RemoveAll(item => item.Id == id) > 0)
        {
            _store.WriteJsonFile(GetMonthFilePath(year, month), file);
        }
    }

    private void DeleteAttachmentFile(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return;
        }
        var path = Path.GetFullPath(Path.Combine(ExpenseDirectory, relative));
        if (IsInsideExpenseDirectory(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private bool IsInsideExpenseDirectory(string path) =>
        path.StartsWith(Path.GetFullPath(ExpenseDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        if (clean.Length == 0)
        {
            clean = "allegato";
        }
        return clean.Length > 80 ? clean[..40] + clean[^40..] : clean;
    }

    private static ExpenseEntry Normalize(ExpenseEntry entry)
    {
        entry.Description ??= string.Empty;
        entry.AttachmentFile ??= string.Empty;
        return entry;
    }
}

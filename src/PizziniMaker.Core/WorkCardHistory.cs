namespace PizziniMaker.Core;

public sealed class WorkCardHistoryEntry
{
    /// <summary>Tipo di riferimento (scheda, mandato, preventivo). Assente nei file 1.2.0: vale Scheda.</summary>
    public ActivityType Type { get; set; } = ActivityType.Scheda;

    public string Number { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public DateOnly? LastUsed { get; set; }

    public int UseCount { get; set; }

    public string Reference => ActivityCatalog.GetWorkCardReference(Type, Number);

    public string DisplayText => string.IsNullOrWhiteSpace(Comment) ? Reference : $"{Reference} — {Comment}";

    public WorkCardHistoryEntry Clone() => new()
    {
        Type = Type,
        Number = Number,
        Comment = Comment,
        LastUsed = LastUsed,
        UseCount = UseCount
    };
}

/// <summary>
/// Storico dei riferimenti utilizzati: una voce per tipo e numero (scheda, mandato o preventivo),
/// con l'ultimo commento noto.
/// </summary>
public sealed class WorkCardHistory
{
    public const int MaximumCommentLength = 60;

    public int SchemaVersion { get; set; } = 2;

    public List<WorkCardHistoryEntry> Entries { get; set; } = [];

    public WorkCardHistoryEntry? Find(string? number) => Find(ActivityType.Scheda, number);

    public WorkCardHistoryEntry? Find(ActivityType type, string? number) =>
        number is null ? null : Entries.FirstOrDefault(entry => entry.Type == type && entry.Number == number.Trim());

    public static bool IsValidEntry(WorkCardHistoryEntry? entry) =>
        entry is not null && entry.Type.HasWorkCard() && WorkDayValidator.IsValidWorkCardNumber(entry.Type, entry.Number);

    public static string NormalizeComment(string? comment)
    {
        var value = (comment ?? string.Empty).Trim();
        return value.Length > MaximumCommentLength ? value[..MaximumCommentLength].TrimEnd() : value;
    }

    /// <summary>Aggiunge o modifica manualmente una scheda. Restituisce la voce aggiornata.</summary>
    public WorkCardHistoryEntry Upsert(string number, string? comment) => Upsert(ActivityType.Scheda, number, comment);

    /// <summary>Aggiunge o modifica manualmente una voce. Restituisce la voce aggiornata.</summary>
    public WorkCardHistoryEntry Upsert(ActivityType type, string number, string? comment)
    {
        number = (number ?? string.Empty).Trim();
        if (!WorkDayValidator.IsValidWorkCardNumber(type, number))
        {
            throw new ArgumentException(char.ToUpperInvariant(WorkDayValidator.GetWorkCardFormatError(type)[0]) +
                WorkDayValidator.GetWorkCardFormatError(type)[1..], nameof(number));
        }

        var entry = Find(type, number);
        if (entry is null)
        {
            entry = new WorkCardHistoryEntry { Type = type, Number = number };
            Entries.Add(entry);
        }

        entry.Comment = NormalizeComment(comment);
        return entry;
    }

    public bool Remove(string number) => Remove(ActivityType.Scheda, number);

    public bool Remove(ActivityType type, string number) =>
        Entries.RemoveAll(entry => entry.Type == type && entry.Number == number) > 0;

    /// <summary>Registra i riferimenti validi di una giornata (una volta per tipo, numero e giornata).</summary>
    public void RecordDay(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var cards = day.Entries
            .Where(entry => entry.Type.HasWorkCard() && WorkDayValidator.IsValidWorkCardNumber(entry.Type, entry.WorkCardNumber))
            .GroupBy(entry => (entry.Type, entry.WorkCardNumber));

        foreach (var card in cards)
        {
            var comment = card.Select(entry => NormalizeComment(entry.WorkCardComment)).LastOrDefault(value => value.Length > 0)
                ?? string.Empty;
            var entry = Find(card.Key.Type, card.Key.WorkCardNumber);
            if (entry is null)
            {
                Entries.Add(new WorkCardHistoryEntry
                {
                    Type = card.Key.Type,
                    Number = card.Key.WorkCardNumber,
                    Comment = comment,
                    LastUsed = day.Date,
                    UseCount = 1
                });
                continue;
            }

            var isMostRecent = entry.LastUsed is null || day.Date >= entry.LastUsed;
            if (comment.Length > 0 && (isMostRecent || entry.Comment.Length == 0))
            {
                entry.Comment = comment;
            }
            if (isMostRecent)
            {
                entry.LastUsed = day.Date;
            }
            entry.UseCount++;
        }
    }

    /// <summary>Fonde un altro storico senza perdere le voci già presenti.</summary>
    public void Merge(WorkCardHistory other)
    {
        ArgumentNullException.ThrowIfNull(other);
        foreach (var incoming in other.Entries.Where(IsValidEntry))
        {
            var entry = Find(incoming.Type, incoming.Number);
            if (entry is null)
            {
                var clone = incoming.Clone();
                clone.Comment = NormalizeComment(clone.Comment);
                Entries.Add(clone);
                continue;
            }

            var incomingIsNewer = incoming.LastUsed is not null && (entry.LastUsed is null || incoming.LastUsed > entry.LastUsed);
            var incomingComment = NormalizeComment(incoming.Comment);
            if (incomingComment.Length > 0 && (entry.Comment.Length == 0 || incomingIsNewer))
            {
                entry.Comment = incomingComment;
            }
            if (incomingIsNewer)
            {
                entry.LastUsed = incoming.LastUsed;
            }
            entry.UseCount = Math.Max(entry.UseCount, incoming.UseCount);
        }
    }

    public static WorkCardHistory Rebuild(IEnumerable<WorkDay> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        var history = new WorkCardHistory();
        foreach (var day in days.Where(day => day.IsConfirmed).OrderBy(day => day.Date))
        {
            history.RecordDay(day);
        }
        return history;
    }

    public IReadOnlyList<WorkCardHistoryEntry> OrderedByRecent() => Entries
        .OrderByDescending(entry => entry.LastUsed ?? DateOnly.MinValue)
        .ThenByDescending(entry => entry.UseCount)
        .ThenBy(entry => entry.Type)
        .ThenBy(entry => entry.Number, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// Ricerca senza distinzione tra maiuscole e minuscole su numero e commento,
    /// ordinata per pertinenza e poi per utilizzo recente. Con <paramref name="type"/> limita a quel tipo.
    /// </summary>
    public IReadOnlyList<WorkCardHistoryEntry> Search(string? query, int maxResults = 8, ActivityType? type = null)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        return Entries
            .Where(entry => type is null || entry.Type == type)
            .Select(entry => (Entry: entry, Rank: GetRank(entry, text)))
            .Where(item => item.Rank >= 0)
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.Entry.LastUsed ?? DateOnly.MinValue)
            .ThenByDescending(item => item.Entry.UseCount)
            .ThenBy(item => item.Entry.Number, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(item => item.Entry)
            .ToArray();
    }

    private static int GetRank(WorkCardHistoryEntry entry, string query)
    {
        const StringComparison ignoreCase = StringComparison.CurrentCultureIgnoreCase;
        if (entry.Number.StartsWith(query, ignoreCase))
        {
            return 0;
        }
        if (entry.Comment.StartsWith(query, ignoreCase))
        {
            return 1;
        }
        if (entry.Comment.Split([' ', '-', '/', '.', ','], StringSplitOptions.RemoveEmptyEntries)
            .Any(word => word.StartsWith(query, ignoreCase)))
        {
            return 2;
        }
        if (entry.Number.Contains(query, ignoreCase) || entry.Comment.Contains(query, ignoreCase) ||
            entry.Type != ActivityType.Scheda && entry.Type.ToDisplayName().StartsWith(query, ignoreCase))
        {
            return 3;
        }
        return -1;
    }
}

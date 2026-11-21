namespace PizziniMaker.Core;

public sealed class WorkCardHistoryEntry
{
    public string Number { get; set; } = string.Empty;

    public string Comment { get; set; } = string.Empty;

    public DateOnly? LastUsed { get; set; }

    public int UseCount { get; set; }

    public string DisplayText => string.IsNullOrWhiteSpace(Comment) ? Number : $"{Number} — {Comment}";

    public WorkCardHistoryEntry Clone() => new()
    {
        Number = Number,
        Comment = Comment,
        LastUsed = LastUsed,
        UseCount = UseCount
    };
}

/// <summary>
/// Storico delle schede utilizzate: una voce per numero di scheda, con l'ultimo commento noto.
/// </summary>
public sealed class WorkCardHistory
{
    public const int MaximumCommentLength = 60;

    public int SchemaVersion { get; set; } = 1;

    public List<WorkCardHistoryEntry> Entries { get; set; } = [];

    public WorkCardHistoryEntry? Find(string? number) =>
        number is null ? null : Entries.FirstOrDefault(entry => entry.Number == number.Trim());

    public static string NormalizeComment(string? comment)
    {
        var value = (comment ?? string.Empty).Trim();
        return value.Length > MaximumCommentLength ? value[..MaximumCommentLength].TrimEnd() : value;
    }

    /// <summary>Aggiunge o modifica manualmente una voce. Restituisce la voce aggiornata.</summary>
    public WorkCardHistoryEntry Upsert(string number, string? comment)
    {
        number = (number ?? string.Empty).Trim();
        if (!WorkDayValidator.IsValidWorkCardNumber(number))
        {
            throw new ArgumentException("Il numero di scheda deve contenere esattamente 4 cifre.", nameof(number));
        }

        var entry = Find(number);
        if (entry is null)
        {
            entry = new WorkCardHistoryEntry { Number = number };
            Entries.Add(entry);
        }

        entry.Comment = NormalizeComment(comment);
        return entry;
    }

    public bool Remove(string number) => Entries.RemoveAll(entry => entry.Number == number) > 0;

    /// <summary>Registra le schede valide di una giornata (una volta per numero e giornata).</summary>
    public void RecordDay(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var cards = day.Entries
            .Where(entry => entry.Type == ActivityType.Scheda && WorkDayValidator.IsValidWorkCardNumber(entry.WorkCardNumber))
            .GroupBy(entry => entry.WorkCardNumber);

        foreach (var card in cards)
        {
            var comment = card.Select(entry => NormalizeComment(entry.WorkCardComment)).LastOrDefault(value => value.Length > 0)
                ?? string.Empty;
            var entry = Find(card.Key);
            if (entry is null)
            {
                Entries.Add(new WorkCardHistoryEntry
                {
                    Number = card.Key,
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
        foreach (var incoming in other.Entries.Where(entry => WorkDayValidator.IsValidWorkCardNumber(entry.Number)))
        {
            var entry = Find(incoming.Number);
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
        .ThenBy(entry => entry.Number, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// Ricerca senza distinzione tra maiuscole e minuscole su numero e commento,
    /// ordinata per pertinenza e poi per utilizzo recente.
    /// </summary>
    public IReadOnlyList<WorkCardHistoryEntry> Search(string? query, int maxResults = 8)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return [];
        }

        return Entries
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
        if (entry.Number.StartsWith(query, StringComparison.Ordinal))
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
        if (entry.Number.Contains(query, StringComparison.Ordinal) || entry.Comment.Contains(query, ignoreCase))
        {
            return 3;
        }
        return -1;
    }
}

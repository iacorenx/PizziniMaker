namespace PizziniMaker.Core;

public static class WorkDayValidator
{
    public const int MaximumDayMinutes = 24 * 60;

    public static IReadOnlyList<string> Validate(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var errors = new List<string>();

        if (day.Entries.Count == 0)
        {
            errors.Add("Inserire almeno una riga di attività.");
            return errors;
        }

        for (var index = 0; index < day.Entries.Count; index++)
        {
            var entry = day.Entries[index];
            var row = index + 1;

            if (entry.Minutes <= 0 || entry.Minutes % 30 != 0)
            {
                errors.Add($"Riga {row}: selezionare una durata valida in incrementi di 30 minuti.");
            }

            if (entry.Type.HasWorkCard() && !IsValidWorkCardNumber(entry.Type, entry.WorkCardNumber))
            {
                errors.Add($"Riga {row}: {GetWorkCardFormatError(entry.Type)}");
            }

            if (entry.Type == ActivityType.Custom && string.IsNullOrWhiteSpace(entry.CustomActivityName))
            {
                errors.Add($"Riga {row}: specificare il nome dell'attività personalizzata.");
            }
        }

        if (day.TotalMinutes > MaximumDayMinutes)
        {
            errors.Add("Il totale della giornata non può superare 24 ore.");
        }

        return errors;
    }

    public static IReadOnlyList<string> ValidateForConfirmation(WorkDay day)
    {
        var errors = Validate(day).ToList();
        if (!DailyHours.IsOptionalDay(day.Date) && day.TotalMinutes < DailyHours.RequiredMinutes)
        {
            errors.Add($"Per chiudere il giorno servono almeno {ActivityCatalog.FormatDuration(DailyHours.RequiredMinutes)}.");
        }

        if (ItalianHolidayCalendar.IsNonWorkingDay(day.Date) && !day.AllowNonWorkingDay)
        {
            errors.Add("Il giorno non lavorativo deve essere abilitato tramite la forzatura dedicata.");
        }

        return errors;
    }

    public const int MaximumReferenceDigits = 6;

    /// <summary>Numero di scheda: esattamente 4 cifre, zeri iniziali compresi.</summary>
    public static bool IsValidWorkCardNumber(string? number) =>
        number is not null && number.Length == 4 && number.All(char.IsAsciiDigit);

    /// <summary>
    /// Scheda <c>1234</c>; mandato <c>123/X</c> (numero/lettera maiuscola); preventivo <c>1234/2026</c> (numero/anno).
    /// </summary>
    public static bool IsValidWorkCardNumber(ActivityType type, string? number)
    {
        if (type == ActivityType.Scheda)
        {
            return IsValidWorkCardNumber(number);
        }
        if (type is not (ActivityType.Mandato or ActivityType.Preventivo) ||
            number is null || number.Count(character => character == '/') != 1)
        {
            return false;
        }

        var (main, suffix) = SplitReference(number);
        if (main.Length is < 1 or > MaximumReferenceDigits || !main.All(char.IsAsciiDigit))
        {
            return false;
        }
        return type == ActivityType.Mandato
            ? suffix.Length == 1 && char.IsAsciiLetterUpper(suffix[0])
            : suffix.Length == 4 && suffix.All(char.IsAsciiDigit);
    }

    public static string GetWorkCardFormatError(ActivityType type) => type switch
    {
        ActivityType.Mandato => "il mandato deve avere il formato numero/lettera, ad esempio 123/X.",
        ActivityType.Preventivo => "il preventivo deve avere il formato numero/anno, ad esempio 1234/2026.",
        _ => "il numero di scheda deve contenere esattamente 4 cifre."
    };

    /// <summary>Compone il riferimento di mandato o preventivo; la lettera del mandato diventa maiuscola.</summary>
    public static string ComposeReference(ActivityType type, string? number, string? suffix)
    {
        var main = (number ?? string.Empty).Trim();
        var tail = (suffix ?? string.Empty).Trim();
        if (type == ActivityType.Mandato)
        {
            tail = tail.ToUpperInvariant();
        }
        return main.Length == 0 && tail.Length == 0 ? string.Empty : $"{main}/{tail}";
    }

    /// <summary>Scompone <c>123/X</c> in numero e suffisso; senza barra tutto il testo è il numero.</summary>
    public static (string Number, string Suffix) SplitReference(string? reference)
    {
        var value = reference ?? string.Empty;
        var separator = value.IndexOf('/');
        return separator < 0 ? (value, string.Empty) : (value[..separator], value[(separator + 1)..]);
    }
}

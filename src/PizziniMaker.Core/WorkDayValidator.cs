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

            if (entry.Type == ActivityType.Scheda && !IsValidWorkCardNumber(entry.WorkCardNumber))
            {
                errors.Add($"Riga {row}: il numero di scheda deve contenere esattamente 4 cifre.");
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

    public static bool IsValidWorkCardNumber(string? number) =>
        number is not null && number.Length == 4 && number.All(char.IsAsciiDigit);
}

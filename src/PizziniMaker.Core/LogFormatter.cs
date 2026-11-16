using System.Text;

namespace PizziniMaker.Core;

public static class LogFormatter
{
    public static string Format(WorkDay day, string employeeName)
    {
        ArgumentNullException.ThrowIfNull(day);
        var builder = new StringBuilder();
        builder.AppendLine($"Dipendente: {employeeName.Trim()}");
        builder.AppendLine($"Giorno: {ActivityCatalog.FormatDate(day.Date)}");
        builder.AppendLine();

        foreach (var entry in day.Entries)
        {
            if (entry.Type == ActivityType.Scheda)
            {
                builder.Append(entry.WorkCardNumber)
                    .Append(" - ")
                    .Append(ActivityCatalog.FormatDuration(entry.Minutes));

                if (entry.IsCad)
                {
                    builder.Append(" (CAD)");
                }

                builder.Append(" - Carta: ")
                    .AppendLine(entry.IsPaperRecorded ? "sì" : "no");
            }
            else
            {
                builder.Append(entry.Type.ToDisplayName())
                    .Append(" - ")
                    .AppendLine(ActivityCatalog.FormatDuration(entry.Minutes));
            }
        }

        builder.AppendLine();
        builder.AppendLine($"Totale: {ActivityCatalog.FormatDuration(day.TotalMinutes)}");
        builder.AppendLine($"Confermato: {day.ConfirmedAt?.ToLocalTime():dd/MM/yyyy HH:mm}");
        return builder.ToString();
    }
}

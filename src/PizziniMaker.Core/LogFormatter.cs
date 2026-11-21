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
        if (day.IsTrasferta)
        {
            builder.AppendLine("Trasferta: sì");
        }
        builder.AppendLine();

        foreach (var entry in day.Entries)
        {
            if (entry.Type == ActivityType.Scheda)
            {
                builder.Append(entry.WorkCardNumber);
                var comment = entry.WorkCardComment?.Trim();
                if (!string.IsNullOrEmpty(comment))
                {
                    builder.Append(" - ").Append(comment);
                }

                builder.Append(" - ")
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
                builder.Append(entry.GetDisplayName())
                    .Append(" - ")
                    .AppendLine(ActivityCatalog.FormatDuration(entry.Minutes));
            }
        }

        builder.AppendLine();
        builder.AppendLine($"Totale: {ActivityCatalog.FormatDuration(day.TotalMinutes)}");
        var overtimeMinutes = DailyHours.GetOvertimeMinutes(day);
        if (overtimeMinutes > 0)
        {
            builder.AppendLine($"Straordinario: {ActivityCatalog.FormatDuration(overtimeMinutes)}");
        }
        var travelMinutes = DailyHours.GetTravelMinutes(day);
        if (travelMinutes > 0)
        {
            builder.AppendLine($"Viaggio: {ActivityCatalog.FormatDuration(travelMinutes)}");
        }
        builder.AppendLine($"Confermato: {day.ConfirmedAt?.ToLocalTime():dd/MM/yyyy HH:mm}");
        return builder.ToString();
    }
}

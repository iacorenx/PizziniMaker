using System.Text;

namespace PizziniMaker.Core;

/// <summary>Riga del foglietto stampato; il quadratino compare solo per le attività con scheda cartacea.</summary>
public sealed record PrintSlipLine(string Text, bool ShowPaperBox, bool PaperChecked);

/// <summary>Contenuto di un foglietto A6: intestazione, elenco attività e totale.</summary>
public sealed record PrintSlip(
    string EmployeeName,
    DateOnly Date,
    IReadOnlyList<PrintSlipLine> Lines,
    string FooterText,
    bool IsConfirmed)
{
    public string DateText => ActivityCatalog.FormatShortDate(Date);
}

public static class PrintSlipBuilder
{
    /// <summary>Quattro A6 (105 × 148 mm) su un A4 verticale.</summary>
    public const int DefaultSlipsPerPage = 4;

    public static PrintSlip Build(WorkDay day, string employeeName)
    {
        ArgumentNullException.ThrowIfNull(day);
        var lines = day.Entries
            .Where(entry => entry.Minutes > 0)
            .Select(entry => new PrintSlipLine(FormatLine(entry), entry.Type.HasWorkCard(), entry.Type.HasWorkCard() && entry.IsPaperRecorded))
            .ToArray();

        var footer = new StringBuilder($"Totale: {ActivityCatalog.FormatDuration(day.TotalMinutes)}");
        var travel = DailyHours.GetTravelMinutes(day);
        if (travel > 0)
        {
            footer.Append($" · viaggio {ActivityCatalog.FormatDuration(travel)}");
        }
        var overtime = DailyHours.GetOvertimeMinutes(day);
        if (overtime > 0)
        {
            footer.Append($" · straordinario {ActivityCatalog.FormatDuration(overtime)}");
        }
        if (day.IsTrasferta)
        {
            footer.Append(" · trasferta");
        }
        if (!day.IsConfirmed)
        {
            footer.Append(" · BOZZA");
        }

        return new PrintSlip((employeeName ?? string.Empty).Trim().ToUpperInvariant(), day.Date, lines, footer.ToString(), day.IsConfirmed);
    }

    /// <summary>Testo della riga: <c>8092 Baccane — 4,5 ore (CAD)</c>, <c>MANDATO 123/X — 2 ore</c>, <c>VIAGGIO — 1,5 ore</c>.</summary>
    public static string FormatLine(ActivityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var text = new StringBuilder();
        if (entry.Type.HasWorkCard())
        {
            text.Append(entry.GetWorkCardReference());
            var comment = entry.WorkCardComment?.Trim();
            if (!string.IsNullOrEmpty(comment))
            {
                text.Append(' ').Append(comment);
            }
        }
        else
        {
            text.Append(entry.GetDisplayName());
        }

        text.Append(" — ").Append(ActivityCatalog.FormatDuration(entry.Minutes));
        if (entry.Type.HasWorkCard() && entry.IsCad)
        {
            text.Append(" (CAD)");
        }
        return text.ToString();
    }

    public static IReadOnlyList<IReadOnlyList<PrintSlip>> Paginate(IEnumerable<PrintSlip> slips, int slipsPerPage = DefaultSlipsPerPage)
    {
        ArgumentNullException.ThrowIfNull(slips);
        ArgumentOutOfRangeException.ThrowIfLessThan(slipsPerPage, 1);
        return slips.OrderBy(slip => slip.Date).Chunk(slipsPerPage).Select(page => (IReadOnlyList<PrintSlip>)page).ToArray();
    }

    public static int CountPages(int slipCount, int slipsPerPage = DefaultSlipsPerPage) =>
        slipCount <= 0 ? 0 : (slipCount + slipsPerPage - 1) / slipsPerPage;
}

/// <summary>Disposizione comune (in millimetri) di stampa, PDF e Word: A4 verticale, griglia 2 × 2 di A6.</summary>
public static class SlipLayout
{
    public const double PageWidthMm = 210;
    public const double PageHeightMm = 297;
    public const int Columns = 2;
    public const int Rows = 2;
    public const double SlotWidthMm = PageWidthMm / Columns;    // 105 mm
    public const double SlotHeightMm = PageHeightMm / Rows;     // 148,5 mm

    /// <summary>Rientro del bordo tratteggiato dal bordo dell'A6, per restare nell'area stampabile.</summary>
    public const double BorderInsetMm = 3;

    public const double PaddingMm = 5;
    public const double HeaderFontPt = 11;
    public const double LineFontPt = 12;
    public const double FooterFontPt = 10;
    public const double PaperBoxMm = 4.2;

    public static (double X, double Y) SlotOrigin(int index) =>
        (index % Columns * SlotWidthMm, index / Columns % Rows * SlotHeightMm);

    public static double PointsToMm(double points) => points * 25.4 / 72;

    public static double MmToPoints(double mm) => mm * 72 / 25.4;
}

public static class TextWrapper
{
    /// <summary>Spezza il testo in righe che non superano <paramref name="maxWidth"/> secondo la misura fornita.</summary>
    public static IReadOnlyList<string> Wrap(string text, double maxWidth, Func<string, double> measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        var lines = new List<string>();
        var current = string.Empty;
        foreach (var word in (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (measure(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            // Parola più lunga della riga: viene spezzata carattere per carattere.
            current = word;
            while (current.Length > 1 && measure(current) > maxWidth)
            {
                var cut = current.Length - 1;
                while (cut > 1 && measure(current[..cut]) > maxWidth)
                {
                    cut--;
                }
                lines.Add(current[..cut]);
                current = current[cut..];
            }
        }

        if (current.Length > 0 || lines.Count == 0)
        {
            lines.Add(current);
        }
        return lines;
    }
}

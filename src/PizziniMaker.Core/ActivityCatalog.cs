using System.Globalization;

namespace PizziniMaker.Core;

public sealed record ActivityTypeOption(ActivityType Value, string Name);

public sealed record HourOption(int Minutes, string Label);

public static class ActivityCatalog
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public static IReadOnlyList<ActivityTypeOption> ActivityTypes { get; } =
    [
        new(ActivityType.Scheda, "Scheda"),
        new(ActivityType.Qu, "QU"),
        new(ActivityType.FeriePermesso, "FERIE/PERMESSO"),
        new(ActivityType.Malattia, "MALATTIA"),
        new(ActivityType.Infortunio, "INFORTUNIO")
    ];

    public static IReadOnlyList<HourOption> HourOptions { get; } = Enumerable
        .Range(1, 24)
        .Select(index => new HourOption(index * 30, FormatHours(index * 30)))
        .ToArray();

    public static string ToDisplayName(this ActivityType type) => type switch
    {
        ActivityType.Scheda => "Scheda",
        ActivityType.Qu => "QU",
        ActivityType.FeriePermesso => "FERIE/PERMESSO",
        ActivityType.Malattia => "MALATTIA",
        ActivityType.Infortunio => "INFORTUNIO",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static string FormatHours(int minutes) =>
        (minutes / 60m).ToString("0.#", ItalianCulture);

    public static string FormatDuration(int minutes)
    {
        var unit = minutes == 60 ? "ora" : "ore";
        return $"{FormatHours(minutes)} {unit}";
    }

    public static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dddd dd/MM/yyyy", ItalianCulture);
}

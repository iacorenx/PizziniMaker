using System.Globalization;

namespace PizziniMaker.Core;

public sealed record ActivitySelection(
    string Key,
    ActivityType Type,
    string Name,
    string Icon,
    string CustomName = "")
{
    public string DisplayName => string.IsNullOrWhiteSpace(Icon) ? Name : $"{Icon} {Name}";
}

public sealed record HourOption(int Minutes, string Label);

public static class ActivityCatalog
{
    private static readonly CultureInfo ItalianCulture = CultureInfo.GetCultureInfo("it-IT");

    public static IReadOnlyList<HourOption> HourOptions { get; } = Enumerable
        .Range(1, 24)
        .Select(index => new HourOption(index * 30, FormatHours(index * 30)))
        .ToArray();

    public static IReadOnlyList<ActivitySelection> GetActivityOptions(
        IEnumerable<CustomActivityDefinition> customActivities)
    {
        ArgumentNullException.ThrowIfNull(customActivities);
        var options = new List<ActivitySelection>
        {
            Builtin("Scheda", ActivityType.Scheda),
            Builtin("Mandato", ActivityType.Mandato),
            Builtin("Preventivo", ActivityType.Preventivo),
            Builtin("Viaggio", ActivityType.Viaggio),
            Builtin("Qu", ActivityType.Qu),
            Builtin("FeriePermesso", ActivityType.FeriePermesso),
            Builtin("Malattia", ActivityType.Malattia),
            Builtin("Infortunio", ActivityType.Infortunio)
        };

        options.AddRange(customActivities
            .Where(activity => !string.IsNullOrWhiteSpace(activity.Name))
            .GroupBy(activity => activity.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(activity => activity.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(activity => new ActivitySelection(
                $"custom:{activity.Name}",
                ActivityType.Custom,
                activity.Name.ToUpperInvariant(),
                activity.Icon,
                activity.Name.ToUpperInvariant())));
        return options;
    }

    private static ActivitySelection Builtin(string key, ActivityType type) =>
        new(key, type, type.ToDisplayName(), type.GetIcon());

    /// <summary>Attività con numero di riferimento, CAD, commento, storico e registrazione cartacea.</summary>
    public static bool HasWorkCard(this ActivityType type) =>
        type is ActivityType.Scheda or ActivityType.Mandato or ActivityType.Preventivo;

    public static string ToDisplayName(this ActivityType type) => type switch
    {
        ActivityType.Scheda => "Scheda",
        ActivityType.Mandato => "MANDATO",
        ActivityType.Preventivo => "PREVENTIVO",
        ActivityType.Qu => "QU",
        ActivityType.FeriePermesso => "FERIE/PERMESSO",
        ActivityType.Malattia => "MALATTIA",
        ActivityType.Infortunio => "INFORTUNIO",
        ActivityType.Custom => "PERSONALIZZATA",
        ActivityType.Viaggio => "VIAGGIO",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static string GetDisplayName(this ActivityEntry entry) => entry.Type == ActivityType.Custom
        ? entry.CustomActivityName
        : entry.Type.ToDisplayName();

    /// <summary>Icona dell'attività: fonte unica per elenco a discesa, calendario e stampa.</summary>
    public static string GetIcon(this ActivityType type) => type switch
    {
        ActivityType.Scheda => "▤",
        ActivityType.Mandato => MandatoIcon,
        ActivityType.Preventivo => PreventivoIcon,
        ActivityType.Qu => "◷",
        ActivityType.FeriePermesso => FeriePermessoIcon,
        ActivityType.Malattia => MalattiaIcon,
        ActivityType.Infortunio => InfortunioIcon,
        ActivityType.Viaggio => TravelIcon,
        _ => string.Empty
    };

    public static string GetIcon(this ActivityEntry entry) => entry.Type == ActivityType.Custom
        ? entry.CustomActivityIcon
        : entry.Type.GetIcon();

    /// <summary>Riferimento leggibile: <c>8092</c>, <c>MANDATO 123/X</c>, <c>PREVENTIVO 1234/2026</c>.</summary>
    public static string GetWorkCardReference(this ActivityEntry entry) =>
        GetWorkCardReference(entry.Type, entry.WorkCardNumber);

    public static string GetWorkCardReference(ActivityType type, string number) => type == ActivityType.Scheda
        ? number
        : $"{type.ToDisplayName()} {number}";

    public const string MandatoIcon = "✎";

    public const string PreventivoIcon = "✉";

    public const string FeriePermessoIcon = "🏝️";

    public const string MalattiaIcon = "✚";

    /// <summary>Simbolo presente nei caratteri di Windows 10 (l'emoji 🩹 non veniva mostrata).</summary>
    public const string InfortunioIcon = "⚠";

    public const string OvertimeIcon = "€";

    public const string TravelIcon = "🚗";

    public const string TrasfertaIcon = "🧳";

    /// <summary>Nomi non utilizzabili per le attività personalizzate.</summary>
    public static IReadOnlyList<string> ReservedActivityNames { get; } =
        ["SCHEDA", "MANDATO", "PREVENTIVO", "QU", "FERIE/PERMESSO", "MALATTIA", "INFORTUNIO", "VIAGGIO",
            AppSettings.LegacyTrasfertaActivityName];

    public static string FormatHours(int minutes) => (minutes / 60m).ToString("0.#", ItalianCulture);

    public static string FormatDuration(int minutes)
    {
        var unit = minutes == 60 ? "ora" : "ore";
        return $"{FormatHours(minutes)} {unit}";
    }

    public static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dddd dd/MM/yyyy", ItalianCulture);

    public static string FormatShortDate(DateOnly date) =>
        date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string FormatEuro(long cents) =>
        (cents / 100m).ToString("#,##0.00", ItalianCulture) + " €";
}

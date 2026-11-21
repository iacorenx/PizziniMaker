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
            new("Scheda", ActivityType.Scheda, "Scheda", "▤"),
            new("Viaggio", ActivityType.Viaggio, "VIAGGIO", TravelIcon),
            new("Qu", ActivityType.Qu, "QU", "◷"),
            new("FeriePermesso", ActivityType.FeriePermesso, "FERIE/PERMESSO", "🏝️"),
            new("Malattia", ActivityType.Malattia, "MALATTIA", "✚"),
            new("Infortunio", ActivityType.Infortunio, "INFORTUNIO", "🩹")
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

    public static string ToDisplayName(this ActivityType type) => type switch
    {
        ActivityType.Scheda => "Scheda",
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

    public static string GetIcon(this ActivityEntry entry) => entry.Type switch
    {
        ActivityType.FeriePermesso => "🏝️",
        ActivityType.Malattia => "✚",
        ActivityType.Infortunio => "🩹",
        ActivityType.Custom => entry.CustomActivityIcon,
        ActivityType.Viaggio => TravelIcon,
        _ => string.Empty
    };

    public const string TravelIcon = "🚗";

    public const string TrasfertaIcon = "🧳";

    /// <summary>Nomi non utilizzabili per le attività personalizzate.</summary>
    public static IReadOnlyList<string> ReservedActivityNames { get; } =
        ["SCHEDA", "QU", "FERIE/PERMESSO", "MALATTIA", "INFORTUNIO", "VIAGGIO", AppSettings.LegacyTrasfertaActivityName];

    public static string FormatHours(int minutes) => (minutes / 60m).ToString("0.#", ItalianCulture);

    public static string FormatDuration(int minutes)
    {
        var unit = minutes == 60 ? "ora" : "ore";
        return $"{FormatHours(minutes)} {unit}";
    }

    public static string FormatDate(DateOnly date) =>
        date.ToDateTime(TimeOnly.MinValue).ToString("dddd dd/MM/yyyy", ItalianCulture);
}

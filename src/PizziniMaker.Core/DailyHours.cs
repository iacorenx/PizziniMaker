namespace PizziniMaker.Core;

public enum DayClosureKind
{
    BelowRequired,
    Exact,
    Overtime,
    OptionalDay
}

public sealed record DayClosureAssessment(
    DayClosureKind Kind,
    int TotalMinutes,
    int MissingMinutes,
    int OvertimeMinutes,
    int TravelMinutes = 0);

public static class DailyHours
{
    public const int RequiredMinutes = 8 * 60;

    /// <summary>
    /// Il sabato è facoltativo: nessun minimo di 8 ore e tutte le ore non di viaggio sono straordinario.
    /// </summary>
    public static bool IsOptionalDay(DateOnly date) => date.DayOfWeek == DayOfWeek.Saturday;

    public static DayClosureAssessment Assess(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var travel = GetTravelMinutes(day);
        var overtime = GetOvertimeMinutes(day);
        if (IsOptionalDay(day.Date))
        {
            return new DayClosureAssessment(DayClosureKind.OptionalDay, day.TotalMinutes, 0, overtime, travel);
        }

        var missing = Math.Max(0, RequiredMinutes - day.TotalMinutes);
        var kind = missing > 0
            ? DayClosureKind.BelowRequired
            : overtime > 0 ? DayClosureKind.Overtime : DayClosureKind.Exact;
        return new DayClosureAssessment(kind, day.TotalMinutes, missing, overtime, travel);
    }

    public static bool IsComplete(WorkDay? day) =>
        day is not null && day.IsConfirmed && (IsOptionalDay(day.Date) || day.TotalMinutes >= RequiredMinutes);

    public static int GetTravelMinutes(WorkDay? day) =>
        day is null ? 0 : day.Entries.Where(entry => entry.Type == ActivityType.Viaggio).Sum(entry => entry.Minutes);

    /// <summary>
    /// Feriali: il viaggio contribuisce alle 8 ore, ma l'eccedenza viene attribuita prima al viaggio,
    /// che ha una contabilità separata e non diventa mai straordinario.
    /// Sabato: tutte le ore non di viaggio sono straordinario.
    /// </summary>
    public static int GetOvertimeMinutes(WorkDay? day)
    {
        if (day is null)
        {
            return 0;
        }

        var workMinutes = day.TotalMinutes - GetTravelMinutes(day);
        return IsOptionalDay(day.Date) ? Math.Max(0, workMinutes) : Math.Max(0, workMinutes - RequiredMinutes);
    }
}

namespace PizziniMaker.Core;

public enum DayClosureKind
{
    BelowRequired,
    Exact,
    Overtime
}

public sealed record DayClosureAssessment(
    DayClosureKind Kind,
    int TotalMinutes,
    int MissingMinutes,
    int OvertimeMinutes);

public static class DailyHours
{
    public const int RequiredMinutes = 8 * 60;

    public static DayClosureAssessment Assess(WorkDay day)
    {
        ArgumentNullException.ThrowIfNull(day);
        var missing = Math.Max(0, RequiredMinutes - day.TotalMinutes);
        var overtime = Math.Max(0, day.TotalMinutes - RequiredMinutes);
        var kind = missing > 0
            ? DayClosureKind.BelowRequired
            : overtime > 0 ? DayClosureKind.Overtime : DayClosureKind.Exact;
        return new DayClosureAssessment(kind, day.TotalMinutes, missing, overtime);
    }

    public static bool IsComplete(WorkDay? day) =>
        day is not null && day.IsConfirmed && day.TotalMinutes >= RequiredMinutes;

    public static int GetOvertimeMinutes(WorkDay? day) =>
        day is null ? 0 : Math.Max(0, day.TotalMinutes - RequiredMinutes);
}

namespace PizziniMaker.Core;

public enum WeekStatusKind
{
    UpToDate,
    Complete,
    Incomplete,
    Future,
    NotStarted
}

public sealed record WeekStatus(WeekStatusKind Kind, IReadOnlyList<DateOnly> MissingDays);

public static class WorkdayCalendar
{
    public static DateOnly GetWeekStart(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    public static IEnumerable<DateOnly> EnumerateWorkdays(DateOnly start, DateOnly end)
    {
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            {
                yield return date;
            }
        }
    }

    public static WeekStatus EvaluateWeek(
        DateOnly displayedDate,
        DateOnly today,
        DateOnly trackingStartDate,
        Func<DateOnly, bool> isConfirmed)
    {
        ArgumentNullException.ThrowIfNull(isConfirmed);

        var weekStart = GetWeekStart(displayedDate);
        var currentWeekStart = GetWeekStart(today);
        var weekEnd = weekStart.AddDays(4);

        if (weekStart > currentWeekStart)
        {
            return new WeekStatus(WeekStatusKind.Future, []);
        }

        if (trackingStartDate > weekEnd)
        {
            return new WeekStatus(WeekStatusKind.NotStarted, []);
        }

        var isCurrentWeek = weekStart == currentWeekStart;
        var firstRequiredDay = trackingStartDate > weekStart ? trackingStartDate : weekStart;
        var lastRequiredDay = isCurrentWeek ? today.AddDays(-1) : weekEnd;

        DateOnly[] missingDays = lastRequiredDay < firstRequiredDay
            ? []
            : EnumerateWorkdays(firstRequiredDay, lastRequiredDay)
                .Where(date => !isConfirmed(date))
                .ToArray();

        var kind = missingDays.Length > 0
            ? WeekStatusKind.Incomplete
            : isCurrentWeek ? WeekStatusKind.UpToDate : WeekStatusKind.Complete;

        return new WeekStatus(kind, missingDays);
    }
}

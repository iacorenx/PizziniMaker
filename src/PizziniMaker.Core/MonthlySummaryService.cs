namespace PizziniMaker.Core;

public sealed record MonthlyDayInfo(
    DateOnly Date,
    bool IsNationalHoliday,
    string? HolidayName,
    bool IsSunday,
    bool IsRequiredWorkday,
    bool IsLocked,
    bool HasSavedData,
    bool IsConfirmed,
    bool IsComplete,
    bool IsIncomplete,
    bool IsToday,
    int TotalMinutes,
    int OvertimeMinutes,
    IReadOnlyList<string> Icons);

public sealed record MonthlyReport(
    int Year,
    int Month,
    IReadOnlyList<MonthlyDayInfo> Days,
    int LeaveDays,
    int LeaveMinutes,
    int SickDays,
    int SickMinutes,
    int OvertimeDays,
    int OvertimeMinutes,
    int MissingDays,
    int MissingMinutes);

public static class MonthlySummaryService
{
    public static MonthlyReport BuildMonth(
        int year,
        int month,
        DateOnly today,
        DateOnly trackingStartDate,
        Func<DateOnly, WorkDay?> loadExistingDay)
    {
        ArgumentNullException.ThrowIfNull(loadExistingDay);
        var firstDay = new DateOnly(year, month, 1);
        var days = new List<MonthlyDayInfo>();
        var leaveDays = 0;
        var leaveMinutes = 0;
        var sickDays = 0;
        var sickMinutes = 0;
        var overtimeDays = 0;
        var overtimeMinutes = 0;
        var missingDays = 0;
        var missingMinutes = 0;

        for (var date = firstDay; date.Month == month; date = date.AddDays(1))
        {
            var day = loadExistingDay(date);
            var isRequired = ItalianHolidayCalendar.IsRequiredWorkday(date) && date >= trackingStartDate;
            var isComplete = DailyHours.IsComplete(day);
            var isIncomplete = isRequired && date < today && !isComplete;
            var overtime = day?.IsConfirmed == true ? DailyHours.GetOvertimeMinutes(day) : 0;
            var holidayName = ItalianHolidayCalendar.GetHolidayName(date);
            var icons = GetIcons(day, overtime);

            days.Add(new MonthlyDayInfo(
                date,
                holidayName is not null,
                holidayName,
                date.DayOfWeek == DayOfWeek.Sunday,
                isRequired,
                ItalianHolidayCalendar.IsNonWorkingDay(date) && day?.AllowNonWorkingDay != true,
                day is not null,
                day?.IsConfirmed == true,
                isComplete,
                isIncomplete,
                date == today,
                day?.TotalMinutes ?? 0,
                overtime,
                icons));

            if (isIncomplete)
            {
                missingDays++;
                missingMinutes += Math.Max(0, DailyHours.RequiredMinutes - (day?.TotalMinutes ?? 0));
            }

            if (day?.IsConfirmed != true)
            {
                continue;
            }

            var dayLeaveMinutes = GetMinutes(day, ActivityType.FeriePermesso);
            var daySickMinutes = GetMinutes(day, ActivityType.Malattia);
            if (dayLeaveMinutes > 0)
            {
                leaveDays++;
                leaveMinutes += dayLeaveMinutes;
            }
            if (daySickMinutes > 0)
            {
                sickDays++;
                sickMinutes += daySickMinutes;
            }
            if (overtime > 0)
            {
                overtimeDays++;
                overtimeMinutes += overtime;
            }
        }

        return new MonthlyReport(year, month, days, leaveDays, leaveMinutes, sickDays,
            sickMinutes, overtimeDays, overtimeMinutes, missingDays, missingMinutes);
    }

    private static int GetMinutes(WorkDay day, ActivityType type) =>
        day.Entries.Where(entry => entry.Type == type).Sum(entry => entry.Minutes);

    private static IReadOnlyList<string> GetIcons(WorkDay? day, int overtime)
    {
        if (day is null)
        {
            return [];
        }

        var icons = new List<string>();
        if (day.Entries.Any(entry => entry.Type == ActivityType.FeriePermesso))
        {
            icons.Add("🏝️");
        }
        if (day.Entries.Any(entry => entry.Type == ActivityType.Malattia))
        {
            icons.Add("✚");
        }
        if (overtime > 0)
        {
            icons.Add("€€");
        }
        icons.AddRange(day.Entries
            .Where(entry => entry.Type == ActivityType.Custom)
            .Select(entry => entry.CustomActivityIcon)
            .Where(icon => !string.IsNullOrWhiteSpace(icon))
            .Distinct());
        return icons;
    }
}

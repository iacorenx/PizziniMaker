using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class MonthlySummaryServiceTests
{
    [Fact]
    public void BuildMonth_ComputesStatusesIconsAndTotals()
    {
        var days = new Dictionary<DateOnly, WorkDay>
        {
            [new DateOnly(2026, 5, 4)] = ConfirmedDay(new DateOnly(2026, 5, 4),
                new ActivityEntry { Type = ActivityType.Custom, CustomActivityName = "TRASFERTA", CustomActivityIcon = "✈️", Minutes = 240 },
                new ActivityEntry { WorkCardNumber = "8000", Minutes = 270 }),
            [new DateOnly(2026, 5, 5)] = ConfirmedDay(new DateOnly(2026, 5, 5),
                new ActivityEntry { Type = ActivityType.FeriePermesso, Minutes = 480 }),
            [new DateOnly(2026, 5, 6)] = ConfirmedDay(new DateOnly(2026, 5, 6),
                new ActivityEntry { Type = ActivityType.Malattia, Minutes = 480 }),
            [new DateOnly(2026, 5, 7)] = WorkDay.CreateNew(new DateOnly(2026, 5, 7))
        };
        days[new DateOnly(2026, 5, 7)].Entries[0].Minutes = 240;

        var report = MonthlySummaryService.BuildMonth(2026, 5, new DateOnly(2026, 5, 11),
            new DateOnly(2026, 1, 1), date => days.GetValueOrDefault(date));

        Assert.Equal(1, report.LeaveDays);
        Assert.Equal(480, report.LeaveMinutes);
        Assert.Equal(1, report.SickDays);
        Assert.Equal(480, report.SickMinutes);
        Assert.Equal(1, report.OvertimeDays);
        Assert.Equal(30, report.OvertimeMinutes);
        Assert.Equal(2, report.MissingDays);
        Assert.Equal(720, report.MissingMinutes);

        var overtimeDay = report.Days.Single(day => day.Date.Day == 4);
        Assert.True(overtimeDay.IsComplete);
        Assert.Contains("€", overtimeDay.Icons);
        Assert.Contains("✈️", overtimeDay.Icons);
        Assert.Contains("🏝️", report.Days.Single(day => day.Date.Day == 5).Icons);
        Assert.Contains("✚", report.Days.Single(day => day.Date.Day == 6).Icons);

        var holiday = report.Days.Single(day => day.Date.Day == 1);
        Assert.True(holiday.IsNationalHoliday);
        Assert.True(holiday.IsLocked);
        Assert.False(holiday.IsRequiredWorkday);
        Assert.True(report.Days.Single(day => day.Date.Day == 10).IsLocked);
        Assert.False(report.Days.Single(day => day.Date.Day == 11).IsIncomplete);
        Assert.False(report.Days.Single(day => day.Date.Day == 12).IsIncomplete);
    }

    [Fact]
    public void ForcedHoliday_CanBeCompleteButIsNotRequired()
    {
        var holidayDate = new DateOnly(2026, 5, 1);
        var holiday = ConfirmedDay(holidayDate, new ActivityEntry { WorkCardNumber = "8000", Minutes = 480 });
        holiday.AllowNonWorkingDay = true;

        var report = MonthlySummaryService.BuildMonth(2026, 5, new DateOnly(2026, 5, 11),
            new DateOnly(2026, 1, 1), date => date == holidayDate ? holiday : null);
        var info = report.Days.Single(day => day.Date == holidayDate);

        Assert.False(info.IsLocked);
        Assert.True(info.IsComplete);
        Assert.False(info.IsRequiredWorkday);
    }

    [Fact]
    public void BuildMonth_CountsTravelTransferAndSaturdayOvertime()
    {
        var monday = ConfirmedDay(new DateOnly(2026, 11, 16),
            new ActivityEntry { WorkCardNumber = "8000", Minutes = 480 },
            new ActivityEntry { Type = ActivityType.Viaggio, Minutes = 120 });
        monday.IsTrasferta = true;
        var tuesday = ConfirmedDay(new DateOnly(2026, 11, 17), new ActivityEntry { WorkCardNumber = "8000", Minutes = 480 });
        tuesday.IsTrasferta = true;
        var saturday = ConfirmedDay(new DateOnly(2026, 11, 21),
            new ActivityEntry { WorkCardNumber = "8000", Minutes = 180 },
            new ActivityEntry { Type = ActivityType.Viaggio, Minutes = 60 });
        var days = new[] { monday, tuesday, saturday }.ToDictionary(day => day.Date);

        var report = MonthlySummaryService.BuildMonth(2026, 11, new DateOnly(2026, 11, 22),
            new DateOnly(2026, 11, 16), date => days.GetValueOrDefault(date));

        Assert.Equal(2, report.TravelDays);
        Assert.Equal(180, report.TravelMinutes);
        Assert.Equal(2, report.TrasfertaDays);
        Assert.Equal(1, report.OvertimeDays);
        Assert.Equal(180, report.OvertimeMinutes);
        var mondayInfo = report.Days.Single(day => day.Date.Day == 16);
        Assert.Contains("🧳", mondayInfo.Icons);
        Assert.Contains("🚗", mondayInfo.Icons);
        Assert.DoesNotContain("€", mondayInfo.Icons);
        var saturdayInfo = report.Days.Single(day => day.Date.Day == 21);
        Assert.True(saturdayInfo.IsComplete);
        Assert.False(saturdayInfo.IsIncomplete);
    }

    private static WorkDay ConfirmedDay(DateOnly date, params ActivityEntry[] entries) => new()
    {
        Date = date,
        IsConfirmed = true,
        Entries = entries.ToList()
    };
}

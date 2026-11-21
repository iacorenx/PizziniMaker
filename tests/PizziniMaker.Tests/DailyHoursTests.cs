using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class DailyHoursTests
{
    [Theory]
    [InlineData(450, DayClosureKind.BelowRequired, 30, 0)]
    [InlineData(480, DayClosureKind.Exact, 0, 0)]
    [InlineData(510, DayClosureKind.Overtime, 0, 30)]
    public void Assess_ComputesMissingAndOvertime(int total, DayClosureKind kind, int missing, int overtime)
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].Minutes = total;

        var result = DailyHours.Assess(day);

        Assert.Equal(kind, result.Kind);
        Assert.Equal(missing, result.MissingMinutes);
        Assert.Equal(overtime, result.OvertimeMinutes);
    }

    [Fact]
    public void ConfirmationValidation_RejectsLessThanEightHours()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 450;

        Assert.Contains(WorkDayValidator.ValidateForConfirmation(day), error => error.Contains("almeno 8 ore"));
    }

    [Fact]
    public void NonWorkingDay_RequiresExplicitOverride()
    {
        var sunday = WorkDay.CreateNew(new DateOnly(2026, 11, 15));
        sunday.Entries[0].WorkCardNumber = "8000";
        sunday.Entries[0].Minutes = 480;

        Assert.Contains(WorkDayValidator.ValidateForConfirmation(sunday), error => error.Contains("forzatura"));
        sunday.AllowNonWorkingDay = true;
        Assert.Empty(WorkDayValidator.ValidateForConfirmation(sunday));
    }

    [Fact]
    public void Saturday_IsOptionalAndAllWorkIsOvertimeExceptTravel()
    {
        var saturday = new WorkDay
        {
            Date = new DateOnly(2026, 11, 14),
            Entries =
            [
                new ActivityEntry { WorkCardNumber = "8000", Minutes = 240 },
                new ActivityEntry { Type = ActivityType.Viaggio, Minutes = 90, IsCad = false }
            ]
        };

        var result = DailyHours.Assess(saturday);

        Assert.Equal(DayClosureKind.OptionalDay, result.Kind);
        Assert.Equal(0, result.MissingMinutes);
        Assert.Equal(240, result.OvertimeMinutes);
        Assert.Equal(90, result.TravelMinutes);
        Assert.Empty(WorkDayValidator.ValidateForConfirmation(saturday));
        saturday.IsConfirmed = true;
        Assert.True(DailyHours.IsComplete(saturday));
    }

    [Theory]
    [InlineData(480, 120, 0)]
    [InlineData(540, 120, 60)]
    [InlineData(360, 180, 0)]
    public void Weekday_TravelCountsForEightHoursButNeverAsOvertime(int workMinutes, int travelMinutes, int expectedOvertime)
    {
        var day = new WorkDay
        {
            Date = new DateOnly(2026, 11, 16),
            Entries =
            [
                new ActivityEntry { WorkCardNumber = "8000", Minutes = workMinutes },
                new ActivityEntry { Type = ActivityType.Viaggio, Minutes = travelMinutes, IsCad = false }
            ]
        };

        var result = DailyHours.Assess(day);

        Assert.Equal(expectedOvertime, result.OvertimeMinutes);
        Assert.Equal(travelMinutes, result.TravelMinutes);
        Assert.Equal(Math.Max(0, 480 - workMinutes - travelMinutes), result.MissingMinutes);
    }

    [Fact]
    public void Weekday_TravelFillsMissingHours()
    {
        var day = new WorkDay
        {
            Date = new DateOnly(2026, 11, 16),
            Entries =
            [
                new ActivityEntry { WorkCardNumber = "8000", Minutes = 360 },
                new ActivityEntry { Type = ActivityType.Viaggio, Minutes = 120, IsCad = false }
            ]
        };

        Assert.Equal(DayClosureKind.Exact, DailyHours.Assess(day).Kind);
        Assert.Empty(WorkDayValidator.ValidateForConfirmation(day));
    }

    [Fact]
    public void IsComplete_RequiresConfirmationAndEightHours()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].Minutes = 480;

        Assert.False(DailyHours.IsComplete(day));
        day.IsConfirmed = true;
        Assert.True(DailyHours.IsComplete(day));
    }
}

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
    public void IsComplete_RequiresConfirmationAndEightHours()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].Minutes = 480;

        Assert.False(DailyHours.IsComplete(day));
        day.IsConfirmed = true;
        Assert.True(DailyHours.IsComplete(day));
    }
}

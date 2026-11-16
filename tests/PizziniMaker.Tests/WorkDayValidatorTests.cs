using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class WorkDayValidatorTests
{
    [Theory]
    [InlineData("8000", true)]
    [InlineData("0012", true)]
    [InlineData("0000", true)]
    [InlineData("", false)]
    [InlineData("123", false)]
    [InlineData("12345", false)]
    [InlineData("12A4", false)]
    [InlineData(" 123", false)]
    [InlineData(null, false)]
    public void WorkCardNumber_MustContainExactlyFourDigits(string? number, bool expected) =>
        Assert.Equal(expected, WorkDayValidator.IsValidWorkCardNumber(number));

    [Fact]
    public void ValidDay_AcceptsLeadingZerosAndHalfHours()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "0012";
        day.Entries[0].Minutes = 270;

        Assert.Empty(WorkDayValidator.Validate(day));
    }

    [Fact]
    public void NonSchedaActivity_DoesNotRequireWorkCard()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].Type = ActivityType.Qu;
        day.Entries[0].WorkCardNumber = string.Empty;

        Assert.Empty(WorkDayValidator.Validate(day));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(45)]
    public void InvalidDuration_IsRejected(int minutes)
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = minutes;

        Assert.Contains(WorkDayValidator.Validate(day), error => error.Contains("durata"));
    }

    [Fact]
    public void EmptyDay_IsRejected()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries.Clear();

        Assert.Contains(WorkDayValidator.Validate(day), error => error.Contains("almeno una riga"));
    }

    [Fact]
    public void DayOver24Hours_IsRejected()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 720;
        day.Entries.Add(new ActivityEntry { WorkCardNumber = "8001", Minutes = 750 });

        Assert.Contains(WorkDayValidator.Validate(day), error => error.Contains("24 ore"));
    }
}

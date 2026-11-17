using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class ItalianHolidayCalendarTests
{
    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    public void EasterSunday_IsCalculated(int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), ItalianHolidayCalendar.GetEasterSunday(year));

    [Fact]
    public void EasterMonday_IsNationalHoliday() =>
        Assert.Equal("Lunedì dell'Angelo", ItalianHolidayCalendar.GetHolidayName(new DateOnly(2026, 4, 6)));

    [Theory]
    [InlineData(2026, 1, 1, "Capodanno")]
    [InlineData(2026, 4, 25, "Festa della Liberazione")]
    [InlineData(2026, 8, 15, "Ferragosto")]
    [InlineData(2026, 12, 26, "Santo Stefano")]
    public void FixedHoliday_HasName(int year, int month, int day, string name) =>
        Assert.Equal(name, ItalianHolidayCalendar.GetHolidayName(new DateOnly(year, month, day)));

    [Fact]
    public void RequiredWorkdays_ExcludeWeekendsAndNationalHolidays()
    {
        Assert.False(ItalianHolidayCalendar.IsRequiredWorkday(new DateOnly(2026, 5, 1)));
        Assert.False(ItalianHolidayCalendar.IsRequiredWorkday(new DateOnly(2026, 5, 9)));
        Assert.False(ItalianHolidayCalendar.IsRequiredWorkday(new DateOnly(2026, 5, 10)));
        Assert.True(ItalianHolidayCalendar.IsRequiredWorkday(new DateOnly(2026, 5, 4)));
    }
}

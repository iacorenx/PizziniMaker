namespace PizziniMaker.Core;

public static class ItalianHolidayCalendar
{
    public static bool IsNationalHoliday(DateOnly date) => GetHolidayName(date) is not null;

    public static bool IsNonWorkingDay(DateOnly date) =>
        date.DayOfWeek == DayOfWeek.Sunday || IsNationalHoliday(date);

    public static bool IsRequiredWorkday(DateOnly date) =>
        date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && !IsNationalHoliday(date);

    public static string? GetHolidayName(DateOnly date)
    {
        var name = (date.Month, date.Day) switch
        {
            (1, 1) => "Capodanno",
            (1, 6) => "Epifania",
            (4, 25) => "Festa della Liberazione",
            (5, 1) => "Festa del Lavoro",
            (6, 2) => "Festa della Repubblica",
            (8, 15) => "Ferragosto",
            (11, 1) => "Ognissanti",
            (12, 8) => "Immacolata Concezione",
            (12, 25) => "Natale",
            (12, 26) => "Santo Stefano",
            _ => null
        };

        return name ?? (date == GetEasterSunday(date.Year).AddDays(1) ? "Lunedì dell'Angelo" : null);
    }

    public static DateOnly GetEasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var daysFromMarch1 = h + l - 7 * m + 114;
        var month = daysFromMarch1 / 31;
        var day = daysFromMarch1 % 31 + 1;
        return new DateOnly(year, month, day);
    }
}

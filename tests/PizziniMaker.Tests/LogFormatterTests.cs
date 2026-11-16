using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class LogFormatterTests
{
    [Fact]
    public void Format_ContainsEmployeeActivitiesCadPaperAndTotal()
    {
        var day = new WorkDay
        {
            Date = new DateOnly(2026, 11, 16),
            IsConfirmed = true,
            ConfirmedAt = new DateTimeOffset(2026, 11, 16, 17, 0, 0, TimeSpan.Zero),
            Entries =
            [
                new ActivityEntry
                {
                    WorkCardNumber = "0012",
                    Minutes = 270,
                    IsCad = true,
                    IsPaperRecorded = true
                },
                new ActivityEntry { Type = ActivityType.Qu, Minutes = 90 }
            ]
        };

        var log = LogFormatter.Format(day, "Mario Rossi");

        Assert.Contains("Dipendente: Mario Rossi", log);
        Assert.Contains("Giorno: lunedì 16/11/2026", log);
        Assert.Contains("0012 - 4,5 ore (CAD) - Carta: sì", log);
        Assert.Contains("QU - 1,5 ore", log);
        Assert.Contains("Totale: 6 ore", log);
    }

    [Fact]
    public void Format_OmitsCadWhenUnchecked()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 60;
        day.Entries[0].IsCad = false;

        var log = LogFormatter.Format(day, "Dipendente");

        Assert.Contains("8000 - 1 ora - Carta: no", log);
        Assert.DoesNotContain("(CAD)", log);
    }
}

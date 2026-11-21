using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class CustomActivitiesTests
{
    [Fact]
    public void DefaultCatalog_ContainsFormationAndTravelButNoLongerTransfer()
    {
        var options = ActivityCatalog.GetActivityOptions(AppSettings.CreateDefaultCustomActivities());

        Assert.DoesNotContain(options, option => option.CustomName == "TRASFERTA");
        Assert.Contains(options, option => option.CustomName == "FORMAZIONE" && option.Icon == "🎓");
        Assert.Contains(options, option => option.Type == ActivityType.Viaggio && option.Name == "VIAGGIO");
    }

    [Fact]
    public void CustomActivity_IsWrittenToLog()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0] = new ActivityEntry
        {
            Type = ActivityType.Custom,
            CustomActivityName = "TRASFERTA",
            CustomActivityIcon = "✈️",
            Minutes = 480
        };

        var log = LogFormatter.Format(day, "Dipendente");

        Assert.Contains("TRASFERTA - 8 ore", log);
    }

    [Fact]
    public void Overtime_IsWrittenToLog()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 540;

        var log = LogFormatter.Format(day, "Dipendente");

        Assert.Contains("Totale: 9 ore", log);
        Assert.Contains("Straordinario: 1 ora", log);
    }
}

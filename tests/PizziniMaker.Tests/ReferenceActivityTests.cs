using PizziniMaker.Core;

namespace PizziniMaker.Tests;

/// <summary>MANDATO (123/X) e PREVENTIVO (1234/2026): formati, storico, log e compatibilità.</summary>
public sealed class ReferenceActivityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PizziniMakerTests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("123/X", true)]
    [InlineData("1/A", true)]
    [InlineData("007/B", true)]
    [InlineData("123456/Z", true)]
    [InlineData("1234567/X", false)]
    [InlineData("123/x", false)]
    [InlineData("123/XY", false)]
    [InlineData("123/1", false)]
    [InlineData("/X", false)]
    [InlineData("123", false)]
    [InlineData("12a/X", false)]
    [InlineData("1/2/X", false)]
    public void Mandato_RequiresNumberSlashUppercaseLetter(string value, bool expected) =>
        Assert.Equal(expected, WorkDayValidator.IsValidWorkCardNumber(ActivityType.Mandato, value));

    [Theory]
    [InlineData("1234/2026", true)]
    [InlineData("1/2025", true)]
    [InlineData("0042/2026", true)]
    [InlineData("1234/26", false)]
    [InlineData("1234/20266", false)]
    [InlineData("/2026", false)]
    [InlineData("1234/X", false)]
    [InlineData("1234", false)]
    public void Preventivo_RequiresNumberSlashYear(string value, bool expected) =>
        Assert.Equal(expected, WorkDayValidator.IsValidWorkCardNumber(ActivityType.Preventivo, value));

    [Fact]
    public void Scheda_KeepsFourDigitRule()
    {
        Assert.True(WorkDayValidator.IsValidWorkCardNumber(ActivityType.Scheda, "0123"));
        Assert.False(WorkDayValidator.IsValidWorkCardNumber(ActivityType.Scheda, "123/X"));
        Assert.False(WorkDayValidator.IsValidWorkCardNumber(ActivityType.Qu, "1234"));
    }

    [Fact]
    public void ComposeAndSplit_RoundTrip()
    {
        Assert.Equal("123/X", WorkDayValidator.ComposeReference(ActivityType.Mandato, " 123 ", "x"));
        Assert.Equal("/2026", WorkDayValidator.ComposeReference(ActivityType.Preventivo, "", "2026"));
        Assert.Equal(string.Empty, WorkDayValidator.ComposeReference(ActivityType.Mandato, "", ""));
        Assert.Equal(("1234", "2026"), WorkDayValidator.SplitReference("1234/2026"));
        Assert.Equal(("12", string.Empty), WorkDayValidator.SplitReference("12"));
    }

    [Fact]
    public void Validation_UsesTypeSpecificMessages()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0] = new ActivityEntry { Type = ActivityType.Mandato, WorkCardNumber = "123/", Minutes = 240 };
        day.Entries.Add(new ActivityEntry { Type = ActivityType.Preventivo, WorkCardNumber = "55/2026", Minutes = 240 });

        var errors = WorkDayValidator.Validate(day);

        Assert.Single(errors);
        Assert.Contains("123/X", errors[0]);
    }

    [Fact]
    public void History_KeepsTypesSeparateAndSearchFilters()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.IsConfirmed = true;
        day.Entries =
        [
            new() { Type = ActivityType.Scheda, WorkCardNumber = "1234", WorkCardComment = "Rossi", Minutes = 120 },
            new() { Type = ActivityType.Mandato, WorkCardNumber = "123/X", WorkCardComment = "Rossi mandato", Minutes = 120 },
            new() { Type = ActivityType.Preventivo, WorkCardNumber = "1234/2026", WorkCardComment = "Rossi preventivo", Minutes = 240 }
        ];

        var history = WorkCardHistory.Rebuild([day]);

        Assert.Equal(3, history.Entries.Count);
        Assert.Equal("Rossi mandato", history.Find(ActivityType.Mandato, "123/X")?.Comment);
        Assert.Null(history.Find("123/X"));
        Assert.Equal(["123/X"], history.Search("Rossi", 8, ActivityType.Mandato).Select(entry => entry.Number));
        Assert.Equal(3, history.Search("Rossi").Count);
        Assert.Equal("PREVENTIVO 1234/2026 — Rossi preventivo", history.Find(ActivityType.Preventivo, "1234/2026")?.DisplayText);
    }

    [Fact]
    public void History_FromVersion12_IsReadAsScheda()
    {
        var store = new PizziniStore(_root);
        File.WriteAllText(store.HistoryFilePath,
            "{\"schemaVersion\":1,\"entries\":[{\"number\":\"8092\",\"comment\":\"Baccane\",\"lastUsed\":\"2026-11-10\",\"useCount\":3}]}");

        var history = store.LoadHistory()!;

        var entry = Assert.Single(history.Entries);
        Assert.Equal(ActivityType.Scheda, entry.Type);
        Assert.Equal("Baccane", history.Find("8092")?.Comment);
    }

    [Fact]
    public void Log_ShowsReferenceTypeAndPaperStatus()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.IsConfirmed = true;
        day.Entries =
        [
            new() { Type = ActivityType.Mandato, WorkCardNumber = "123/X", WorkCardComment = "Bianchi", Minutes = 240, IsCad = true, IsPaperRecorded = true },
            new() { Type = ActivityType.Preventivo, WorkCardNumber = "1234/2026", Minutes = 240, IsCad = false }
        ];

        var log = LogFormatter.Format(day, "Mario Rossi");

        Assert.Contains("MANDATO 123/X - Bianchi - 4 ore (CAD) - Carta: sì", log);
        Assert.Contains("PREVENTIVO 1234/2026 - 4 ore - Carta: no", log);
    }

    [Fact]
    public void EnumValues_AreAppendedForCompatibility()
    {
        Assert.Equal(6, (int)ActivityType.Viaggio);
        Assert.Equal(7, (int)ActivityType.Mandato);
        Assert.Equal(8, (int)ActivityType.Preventivo);
    }

    [Fact]
    public void Catalog_ListsMandatoAndPreventivoWithVisibleIcons()
    {
        var options = ActivityCatalog.GetActivityOptions([]);

        Assert.Contains(options, option => option.Type == ActivityType.Mandato && option.Name == "MANDATO");
        Assert.Contains(options, option => option.Type == ActivityType.Preventivo && option.Name == "PREVENTIVO");
        Assert.Equal("⚠", options.Single(option => option.Type == ActivityType.Infortunio).Icon);
        Assert.All(options, option => Assert.False(string.IsNullOrEmpty(option.Icon)));
    }

    [Fact]
    public void SettingsMigration_ToSchema4_RemovesCustomDuplicatesAndKeepsEmployeeId()
    {
        var store = new PizziniStore(_root);
        File.WriteAllText(store.SettingsFilePath,
            "{\"schemaVersion\":3,\"employeeName\":\"Dipendente\",\"trackingStartDate\":\"2026-01-01\"," +
            "\"customActivities\":[{\"name\":\"Mandato\",\"icon\":\"📄\"},{\"name\":\"CANTIERE\",\"icon\":\"🛠️\"}]}");

        var first = store.LoadSettings();
        var second = new PizziniStore(_root).LoadSettings();

        Assert.Equal(4, first.SchemaVersion);
        Assert.Equal(["CANTIERE"], first.CustomActivities.Select(activity => activity.Name));
        Assert.NotEqual(Guid.Empty, first.EmployeeId);
        Assert.Equal(first.EmployeeId, second.EmployeeId);
        Assert.True(first.CheckUpdatesAtStartup);
        Assert.Equal(0, first.KmRateCents);
    }

    [Fact]
    public void SavingDay_IncrementsRevision()
    {
        var store = new PizziniStore(_root);
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        store.SaveDay(day);
        store.SaveDay(day);

        Assert.Equal(2, store.LoadDay(day.Date).Revision);
    }

    [Fact]
    public void MonthlySummary_ShowsInjuryAndSingleEuroOvertime()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        day.IsConfirmed = true;
        day.Entries =
        [
            new() { Type = ActivityType.Infortunio, Minutes = 240 },
            new() { Type = ActivityType.Scheda, WorkCardNumber = "1234", Minutes = 300 }
        ];

        var report = MonthlySummaryService.BuildMonth(2026, 11, new DateOnly(2026, 11, 30), new DateOnly(2026, 11, 1),
            value => value == date ? day : null);
        var info = report.Days.Single(item => item.Date == date);

        Assert.Contains("⚠", info.Icons);
        Assert.Contains("€", info.Icons);
        Assert.DoesNotContain("€€", info.Icons);
        Assert.Equal(1, report.InjuryDays);
        Assert.Equal(240, report.InjuryMinutes);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}

using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class PizziniStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "PizziniMakerTests", Guid.NewGuid().ToString("N"));
    private readonly PizziniStore _store;

    public PizziniStoreTests() => _store = new PizziniStore(_root);

    [Fact]
    public void Settings_ArePersistent()
    {
        var settings = new AppSettings
        {
            EmployeeName = "Mario Rossi",
            TrackingStartDate = new DateOnly(2026, 1, 1),
            AlwaysOnTop = true,
            LaunchAtStartup = true
        };

        _store.SaveSettings(settings);
        var loaded = new PizziniStore(_root).LoadSettings();

        Assert.Equal("Mario Rossi", loaded.EmployeeName);
        Assert.Equal(settings.TrackingStartDate, loaded.TrackingStartDate);
        Assert.True(loaded.AlwaysOnTop);
        Assert.True(loaded.LaunchAtStartup);
    }

    [Fact]
    public void ConfirmedDay_GeneratesLogAndPaperUpdateRewritesIt()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 480;
        _store.SaveDay(day);

        var confirmed = _store.ConfirmDay(date, "Mario Rossi");
        var logBefore = File.ReadAllText(_store.GetLogFilePath(date));
        _store.UpdatePaperStatus(date, confirmed.Entries[0].Id, true, "Mario Rossi");
        var logAfter = File.ReadAllText(_store.GetLogFilePath(date));

        Assert.True(_store.LoadDay(date).IsConfirmed);
        Assert.Contains("Carta: no", logBefore);
        Assert.Contains("Carta: sì", logAfter);
        Assert.Contains("8000 - 8 ore (CAD)", logAfter);
    }

    [Fact]
    public void ReopeningDay_RemovesConfirmationAndStaleLog()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 480;
        _store.SaveDay(day);
        _store.ConfirmDay(date, "Dipendente");

        var reopened = _store.ReopenDay(date);

        Assert.False(reopened.IsConfirmed);
        Assert.Null(reopened.ConfirmedAt);
        Assert.False(File.Exists(_store.GetLogFilePath(date)));
    }

    [Fact]
    public void CorruptedMainFile_IsRecoveredFromBackup()
    {
        var date = new DateOnly(2026, 11, 16);
        var original = WorkDay.CreateNew(date);
        original.Entries[0].WorkCardNumber = "8000";
        _store.SaveDay(original);
        original.Entries[0].WorkCardNumber = "8125";
        _store.SaveDay(original);

        var path = Path.Combine(_store.DataDirectory, "2026-11-16.json");
        File.WriteAllText(path, "JSON non valido");
        var recovered = _store.LoadDay(date);

        Assert.Equal("8000", recovered.Entries[0].WorkCardNumber);
    }

    [Fact]
    public void MissingDays_ExcludeWeekendAndToday()
    {
        var friday = new DateOnly(2026, 11, 13);
        var monday = new DateOnly(2026, 11, 16);

        var missing = _store.GetMissingWorkdays(monday, friday);

        Assert.Equal([friday], missing);
    }

    [Fact]
    public void InvalidDay_CannotBeConfirmed()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        _store.SaveDay(day);

        Assert.Throws<InvalidOperationException>(() => _store.ConfirmDay(date, "Dipendente"));
        Assert.False(File.Exists(_store.GetLogFilePath(date)));
    }

    [Fact]
    public void LessThanEightHours_CannotBeConfirmedEvenWithValidCard()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 240;
        _store.SaveDay(day);

        Assert.Throws<InvalidOperationException>(() => _store.ConfirmDay(date, "Dipendente"));
        Assert.False(_store.LoadDay(date).IsConfirmed);
    }

    [Fact]
    public void OldSettings_GetDefaultCustomActivities()
    {
        File.WriteAllText(_store.SettingsFilePath,
            "{\"schemaVersion\":1,\"employeeName\":\"Dipendente\",\"trackingStartDate\":\"2026-01-01\"}");

        var settings = _store.LoadSettings();

        Assert.Equal(3, settings.SchemaVersion);
        Assert.Contains(settings.CustomActivities, activity => activity.Name == "FORMAZIONE");
        Assert.DoesNotContain(settings.CustomActivities, activity => activity.Name == "TRASFERTA");
    }

    [Fact]
    public void SettingsV2_MigrationRemovesTransferActivityButKeepsOthers()
    {
        File.WriteAllText(_store.SettingsFilePath,
            "{\"schemaVersion\":2,\"employeeName\":\"Dipendente\",\"trackingStartDate\":\"2026-01-01\"," +
            "\"customActivities\":[{\"name\":\"TRASFERTA\",\"icon\":\"✈️\"},{\"name\":\"CANTIERE\",\"icon\":\"🛠️\"}]}");

        var settings = _store.LoadSettings();

        Assert.Equal(3, settings.SchemaVersion);
        Assert.Equal(["CANTIERE"], settings.CustomActivities.Select(activity => activity.Name));
    }

    [Fact]
    public void LegacyDayWithTransferRow_IsLoadedUnchanged()
    {
        var date = new DateOnly(2026, 11, 16);
        Directory.CreateDirectory(_store.DataDirectory);
        File.WriteAllText(Path.Combine(_store.DataDirectory, "2026-11-16.json"),
            "{\"date\":\"2026-11-16\",\"isConfirmed\":true,\"allowNonWorkingDay\":false,\"entries\":[" +
            "{\"type\":5,\"minutes\":480,\"customActivityName\":\"TRASFERTA\",\"customActivityIcon\":\"✈️\"}]}");

        var day = _store.LoadDay(date);

        Assert.Equal(480, day.TotalMinutes);
        Assert.Equal("TRASFERTA", day.Entries[0].CustomActivityName);
        Assert.False(day.IsTrasferta);
        Assert.Equal(string.Empty, day.Entries[0].WorkCardComment);
    }

    [Fact]
    public void WorkCardComment_IsPersistedAndWrittenToLog()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = "8092";
        day.Entries[0].WorkCardComment = "Baccane";
        day.Entries[0].Minutes = 480;
        _store.SaveDay(day);

        _store.ConfirmDay(date, "Dipendente");

        Assert.Equal("Baccane", new PizziniStore(_root).LoadDay(date).Entries[0].WorkCardComment);
        Assert.Contains("8092 - Baccane - 8 ore (CAD) - Carta: no", File.ReadAllText(_store.GetLogFilePath(date)));
    }

    [Fact]
    public void ConfirmDay_UpdatesWorkCardHistory()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = "8092";
        day.Entries[0].WorkCardComment = "Baccane";
        day.Entries[0].Minutes = 480;
        _store.SaveDay(day);

        _store.ConfirmDay(date, "Dipendente");
        var history = _store.LoadHistory();

        Assert.NotNull(history);
        var entry = Assert.Single(history.Entries);
        Assert.Equal("8092", entry.Number);
        Assert.Equal("Baccane", entry.Comment);
        Assert.Equal(date, entry.LastUsed);
    }

    [Fact]
    public void MissingHistory_IsRebuiltFromConfirmedDays()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "1234";
        day.Entries[0].WorkCardComment = "Officina";
        day.IsConfirmed = true;
        _store.SaveDay(day);
        var draft = WorkDay.CreateNew(new DateOnly(2026, 11, 17));
        draft.Entries[0].WorkCardNumber = "9999";
        _store.SaveDay(draft);

        var history = _store.LoadOrRebuildHistory();

        Assert.Equal(["1234"], history.Entries.Select(entry => entry.Number));
        Assert.True(File.Exists(_store.HistoryFilePath));
    }

    [Fact]
    public void SaturdayBelowEightHours_CanBeConfirmed()
    {
        var saturday = new DateOnly(2026, 11, 14);
        var day = WorkDay.CreateNew(saturday);
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 240;
        _store.SaveDay(day);

        var confirmed = _store.ConfirmDay(saturday, "Dipendente");

        Assert.True(confirmed.IsConfirmed);
        Assert.Contains("Straordinario: 4 ore", File.ReadAllText(_store.GetLogFilePath(saturday)));
    }

    [Fact]
    public void HistoricalSundayContent_EnablesExistingDayButEmptyDraftRemainsLocked()
    {
        var sunday = new DateOnly(2026, 11, 15);
        var historical = WorkDay.CreateNew(sunday);
        historical.Entries[0].WorkCardNumber = "8000";
        historical.Entries[0].Minutes = 480;
        _store.SaveDay(historical);
        var path = Path.Combine(_store.DataDirectory, "2026-11-15.json");
        var legacyJson = File.ReadAllText(path).Replace("\"allowNonWorkingDay\": false,", string.Empty);
        File.WriteAllText(path, legacyJson);

        Assert.True(_store.LoadDay(sunday).AllowNonWorkingDay);

        var followingSunday = new DateOnly(2026, 11, 22);
        _store.SaveDay(WorkDay.CreateNew(followingSunday));
        Assert.False(_store.LoadDay(followingSunday).AllowNonWorkingDay);
    }

    [Fact]
    public void Sunday_CannotBeConfirmedWithoutStoredOverride()
    {
        var sunday = new DateOnly(2026, 11, 15);
        var day = WorkDay.CreateNew(sunday);
        day.Entries[0].WorkCardNumber = "8000";
        day.Entries[0].Minutes = 480;
        _store.SaveDay(day);

        Assert.Throws<InvalidOperationException>(() => _store.ConfirmDay(sunday, "Dipendente"));
        Assert.False(_store.LoadDay(sunday).AllowNonWorkingDay);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}

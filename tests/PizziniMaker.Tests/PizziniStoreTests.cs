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
        day.Entries[0].Minutes = 240;
        _store.SaveDay(day);

        var confirmed = _store.ConfirmDay(date, "Mario Rossi");
        var logBefore = File.ReadAllText(_store.GetLogFilePath(date));
        _store.UpdatePaperStatus(date, confirmed.Entries[0].Id, true, "Mario Rossi");
        var logAfter = File.ReadAllText(_store.GetLogFilePath(date));

        Assert.True(_store.LoadDay(date).IsConfirmed);
        Assert.Contains("Carta: no", logBefore);
        Assert.Contains("Carta: sì", logAfter);
        Assert.Contains("8000 - 4 ore (CAD)", logAfter);
    }

    [Fact]
    public void ReopeningDay_RemovesConfirmationAndStaleLog()
    {
        var date = new DateOnly(2026, 11, 16);
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = "8000";
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

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}

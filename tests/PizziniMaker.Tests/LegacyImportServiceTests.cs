using System.IO.Compression;
using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class LegacyImportServiceTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "PizziniMakerImportTests", Guid.NewGuid().ToString("N"));
    private readonly string _sourceRoot;
    private readonly string _targetRoot;
    private readonly PizziniStore _source;
    private readonly PizziniStore _target;

    public LegacyImportServiceTests()
    {
        _sourceRoot = Path.Combine(_base, "Vecchia", "PizziniMaker");
        _targetRoot = Path.Combine(_base, "Nuova", "PizziniMaker");
        _source = new PizziniStore(_sourceRoot);
        _target = new PizziniStore(_targetRoot);
    }

    [Fact]
    public void Import_AddsMissingDaysAndKeepsCurrentOnConflict()
    {
        SaveConfirmed(_source, new DateOnly(2026, 11, 2), "1111", "Vecchia");
        SaveConfirmed(_source, new DateOnly(2026, 11, 3), "2222", "Conflitto vecchio");
        SaveConfirmed(_target, new DateOnly(2026, 11, 3), "3333", "Attuale");

        var preview = LegacyImportService.Analyze(_sourceRoot, _target);
        var report = LegacyImportService.Execute(preview, _target, new AppSettings(), new ImportOptions());

        Assert.Equal([new DateOnly(2026, 11, 2)], preview.NewDays.Select(day => day.Date));
        Assert.Equal([new DateOnly(2026, 11, 3)], preview.ConflictingDays);
        Assert.Equal([new DateOnly(2026, 11, 2)], report.ImportedDays);
        Assert.Equal([new DateOnly(2026, 11, 3)], report.KeptDays);
        Assert.Equal("1111", _target.LoadDay(new DateOnly(2026, 11, 2)).Entries[0].WorkCardNumber);
        Assert.Equal("3333", _target.LoadDay(new DateOnly(2026, 11, 3)).Entries[0].WorkCardNumber);
    }

    [Fact]
    public void Import_CopiesLogsOnlyForImportedDays()
    {
        SaveConfirmed(_source, new DateOnly(2026, 11, 2), "1111", "Vecchia");
        SaveConfirmed(_source, new DateOnly(2026, 11, 3), "2222", "Conflitto");
        SaveConfirmed(_target, new DateOnly(2026, 11, 3), "3333", "Attuale");
        var currentLog = File.ReadAllText(_target.GetLogFilePath(new DateOnly(2026, 11, 3)));

        var preview = LegacyImportService.Analyze(_sourceRoot, _target);
        var report = LegacyImportService.Execute(preview, _target, new AppSettings(), new ImportOptions());

        Assert.Equal(1, report.ImportedLogs);
        Assert.Contains("1111 - Vecchia", File.ReadAllText(_target.GetLogFilePath(new DateOnly(2026, 11, 2))));
        Assert.Equal(currentLog, File.ReadAllText(_target.GetLogFilePath(new DateOnly(2026, 11, 3))));
    }

    [Fact]
    public void Import_RecoversFromBackupAndReportsCorruptedFiles()
    {
        var recovered = new DateOnly(2026, 11, 4);
        var day = WorkDay.CreateNew(recovered);
        day.Entries[0].WorkCardNumber = "4444";
        _source.SaveDay(day);
        _source.SaveDay(day);
        File.WriteAllText(_source.GetDayFilePath(recovered), "JSON non valido");
        File.WriteAllText(_source.GetDayFilePath(new DateOnly(2026, 11, 5)), "{ rotto");
        File.WriteAllText(Path.Combine(_source.DataDirectory, "appunti.json"), "{}");

        var preview = LegacyImportService.Analyze(_sourceRoot, _target);
        LegacyImportService.Execute(preview, _target, new AppSettings(), new ImportOptions());

        Assert.Equal("4444", _target.LoadDay(recovered).Entries[0].WorkCardNumber);
        Assert.False(_target.DayExists(new DateOnly(2026, 11, 5)));
        Assert.Contains(preview.InvalidItems, issue => issue.Item.Contains("2026-11-05"));
        Assert.Contains(preview.InvalidItems, issue => issue.Item.Contains("appunti.json"));
    }

    [Fact]
    public void Import_MergesHistoryAndRebuildsFromImportedDays()
    {
        SaveConfirmed(_source, new DateOnly(2026, 11, 2), "8092", "Baccane");
        var sourceHistory = new WorkCardHistory();
        sourceHistory.Upsert("5555", "Deposito");
        _source.SaveHistory(sourceHistory);
        var targetHistory = new WorkCardHistory();
        targetHistory.Upsert("1234", "Officina");
        _target.SaveHistory(targetHistory);

        var preview = LegacyImportService.Analyze(_sourceRoot, _target);
        var report = LegacyImportService.Execute(preview, _target, new AppSettings(), new ImportOptions());
        var history = _target.LoadHistory()!;

        Assert.Equal(2, report.HistoryEntriesAdded);
        Assert.Equal("Baccane", history.Find("8092")!.Comment);
        Assert.Equal("Deposito", history.Find("5555")!.Comment);
        Assert.Equal("Officina", history.Find("1234")!.Comment);
    }

    [Fact]
    public void Import_CreatesBackupAndDoesNotModifySource()
    {
        SaveConfirmed(_source, new DateOnly(2026, 11, 2), "1111", "Vecchia");
        SaveConfirmed(_target, new DateOnly(2026, 11, 3), "3333", "Attuale");
        var sourceFilesBefore = SnapshotFiles(_sourceRoot);

        var preview = LegacyImportService.Analyze(_sourceRoot, _target);
        var report = LegacyImportService.Execute(preview, _target, new AppSettings(), new ImportOptions());

        Assert.True(File.Exists(report.BackupPath));
        using (var archive = ZipFile.OpenRead(report.BackupPath))
        {
            Assert.Contains(archive.Entries, entry => entry.FullName == "Dati/2026-11-03.json");
        }
        Assert.Equal(sourceFilesBefore, SnapshotFiles(_sourceRoot));
    }

    [Fact]
    public void Import_SettingsAreImportedOnlyWhenRequested()
    {
        _source.SaveSettings(new AppSettings
        {
            EmployeeName = "Mario Rossi",
            TrackingStartDate = new DateOnly(2025, 1, 1),
            CustomActivities = [new() { Name = "CANTIERE", Icon = "🛠️" }, new() { Name = "TRASFERTA", Icon = "✈️" }]
        });
        var current = new AppSettings { EmployeeName = "Mario Rossi", TrackingStartDate = new DateOnly(2026, 6, 1) };

        var preview = LegacyImportService.Analyze(_sourceRoot, _target, current);
        LegacyImportService.Execute(preview, _target, current, new ImportOptions());
        Assert.DoesNotContain(current.CustomActivities, activity => activity.Name == "CANTIERE");
        Assert.Equal(new DateOnly(2026, 6, 1), current.TrackingStartDate);

        var report = LegacyImportService.Execute(preview, _target, current, new ImportOptions(ImportSettings: true));
        Assert.Contains(current.CustomActivities, activity => activity.Name == "CANTIERE");
        Assert.DoesNotContain(current.CustomActivities, activity => activity.Name == "TRASFERTA");
        Assert.Equal(new DateOnly(2025, 1, 1), current.TrackingStartDate);
        Assert.Equal(1, report.CustomActivitiesAdded);
    }

    [Fact]
    public void Analyze_RejectsCurrentFolderAndNestedFolders()
    {
        Assert.Throws<InvalidOperationException>(() => LegacyImportService.Analyze(_targetRoot, _target));
        Assert.Throws<InvalidOperationException>(() => LegacyImportService.Analyze(_targetRoot + Path.DirectorySeparatorChar, _target));
        Assert.Throws<InvalidOperationException>(() => LegacyImportService.Analyze(Path.GetDirectoryName(_targetRoot)!, _target));
        var nested = Path.Combine(_targetRoot, "Copia");
        Directory.CreateDirectory(Path.Combine(nested, "Dati"));
        Assert.Throws<InvalidOperationException>(() => LegacyImportService.Analyze(nested, _target));
    }

    [Fact]
    public void Analyze_RejectsFolderWithoutDataAndAcceptsDataSubfolder()
    {
        var empty = Path.Combine(_base, "Vuota");
        Directory.CreateDirectory(empty);
        SaveConfirmed(_source, new DateOnly(2026, 11, 2), "1111", "Vecchia");

        Assert.Throws<InvalidOperationException>(() => LegacyImportService.Analyze(empty, _target));
        var preview = LegacyImportService.Analyze(Path.Combine(_sourceRoot, "Dati"), _target);
        Assert.Equal(Path.GetFullPath(_sourceRoot), preview.SourceRoot);
        Assert.Single(preview.NewDays);
    }

    private static void SaveConfirmed(PizziniStore store, DateOnly date, string number, string comment)
    {
        var day = WorkDay.CreateNew(date);
        day.Entries[0].WorkCardNumber = number;
        day.Entries[0].WorkCardComment = comment;
        day.Entries[0].Minutes = 480;
        store.SaveDay(day);
        store.ConfirmDay(date, "Dipendente");
    }

    private static string[] SnapshotFiles(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Select(file => $"{Path.GetRelativePath(root, file)}|{File.GetLastWriteTimeUtc(file):O}|{new FileInfo(file).Length}")
        .Order()
        .ToArray();

    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, true);
        }
    }
}

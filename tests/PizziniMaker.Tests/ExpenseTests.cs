using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class ExpenseTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "PizziniMakerExpenseTests", Guid.NewGuid().ToString("N"));
    private readonly PizziniStore _store;
    private readonly ExpenseStore _expenses;

    public ExpenseTests()
    {
        _store = new PizziniStore(Path.Combine(_base, "App"));
        _expenses = new ExpenseStore(_store);
    }

    [Theory]
    [InlineData("12,50", 1250)]
    [InlineData("12.50", 1250)]
    [InlineData("12.5", 1250)]
    [InlineData("1.234,56", 123456)]
    [InlineData("1.234", 123400)]
    [InlineData("€ 8", 800)]
    [InlineData("0,42", 42)]
    public void TryParseEuro_AcceptsItalianFormats(string text, long expected)
    {
        Assert.True(ExpenseCalculator.TryParseEuro(text, out var cents));
        Assert.Equal(expected, cents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    public void TryParseEuro_RejectsInvalidValues(string text) =>
        Assert.False(ExpenseCalculator.TryParseEuro(text, out _));

    [Fact]
    public void KmAmount_UsesRateAtEntryTime()
    {
        var entry = new ExpenseEntry { Category = ExpenseCategory.Chilometri, Km = 120.5m, KmRateCents = 42 };

        Assert.Equal(5061, entry.TotalCents); // 120,5 × 0,42 = 50,61 €
        Assert.Equal(0, ExpenseCalculator.GetKmAmountCents(10, 0));
    }

    [Fact]
    public void Validate_RequiresAmountOrKmAndRate()
    {
        Assert.NotEmpty(ExpenseCalculator.Validate(new ExpenseEntry { Category = ExpenseCategory.Pasto }));
        Assert.NotEmpty(ExpenseCalculator.Validate(new ExpenseEntry { Category = ExpenseCategory.Chilometri, Km = 10 }));
        Assert.NotEmpty(ExpenseCalculator.Validate(new ExpenseEntry { Category = ExpenseCategory.Altro, AmountCents = 100 }));
        Assert.Empty(ExpenseCalculator.Validate(new ExpenseEntry { Category = ExpenseCategory.Pasto, AmountCents = 1200 }));
    }

    [Fact]
    public void SaveLoadAndSummary_PerMonth()
    {
        _expenses.Save(Meal(new DateOnly(2026, 11, 16), 1250));
        _expenses.Save(new ExpenseEntry
        {
            Date = new DateOnly(2026, 11, 17), Category = ExpenseCategory.Chilometri, Km = 100, KmRateCents = 40,
            Status = ExpenseStatus.Rimborsata
        });
        _expenses.Save(Meal(new DateOnly(2026, 12, 1), 900));

        var november = _expenses.LoadMonth(2026, 11);
        var summary = ExpenseCalculator.Summarize(november);

        Assert.Equal(2, november.Count);
        Assert.Equal(5250, summary.TotalCents);
        Assert.Equal(1250, summary.ToReimburseCents);
        Assert.Equal(4000, summary.ReimbursedCents);
        Assert.Equal(100, summary.Km);
        Assert.Single(_expenses.LoadDay(new DateOnly(2026, 11, 16)));
        Assert.Equal([(2026, 11), (2026, 12)], _expenses.EnumerateMonths());
        Assert.True(File.Exists(Path.Combine(_store.ExpenseDirectory, "2026-11.json")));
    }

    [Fact]
    public void ChangingMonth_MovesEntryToNewFile()
    {
        var entry = Meal(new DateOnly(2026, 11, 30), 1000);
        _expenses.Save(entry);
        var previous = entry.Date;
        entry.Date = new DateOnly(2026, 12, 1);
        _expenses.Save(entry, previous);

        Assert.Empty(_expenses.LoadMonth(2026, 11));
        Assert.Single(_expenses.LoadMonth(2026, 12));
    }

    [Fact]
    public void Attachment_IsCopiedAndDeletedWithExpense()
    {
        var receipt = Path.Combine(_base, "scontrino.pdf");
        Directory.CreateDirectory(_base);
        File.WriteAllText(receipt, "pdf");
        var entry = Meal(new DateOnly(2026, 11, 16), 1500);

        _expenses.CopyAttachment(entry, receipt);
        _expenses.Save(entry);
        var path = _expenses.GetAttachmentPath(_expenses.LoadMonth(2026, 11).Single());

        Assert.NotNull(path);
        Assert.StartsWith(Path.Combine(_store.ExpenseDirectory, ExpenseStore.AttachmentsFolderName), path);
        Assert.True(File.Exists(receipt));

        _expenses.Delete(entry);
        Assert.False(File.Exists(path));
        Assert.Empty(_expenses.LoadMonth(2026, 11));
    }

    [Fact]
    public void SetStatus_RecordsReimbursementDate()
    {
        var entry = Meal(new DateOnly(2026, 11, 16), 1500);
        _expenses.Save(entry);
        _expenses.SetStatus(entry, ExpenseStatus.Rimborsata, new DateOnly(2026, 12, 10));
        var loaded = _expenses.LoadMonth(2026, 11).Single();

        Assert.Equal(ExpenseStatus.Rimborsata, loaded.Status);
        Assert.Equal(new DateOnly(2026, 12, 10), loaded.ReimbursedOn);
    }

    [Fact]
    public void Import_CopiesNewExpensesWithAttachmentsAndBackupIncludesThem()
    {
        var sourceStore = new PizziniStore(Path.Combine(_base, "Vecchia"));
        var source = new ExpenseStore(sourceStore);
        var receipt = Path.Combine(_base, "ricevuta.jpg");
        File.WriteAllText(receipt, "jpg");
        var withAttachment = Meal(new DateOnly(2026, 10, 5), 2000);
        source.CopyAttachment(withAttachment, receipt);
        source.Save(withAttachment);
        var shared = Meal(new DateOnly(2026, 10, 6), 700);
        source.Save(shared);
        _expenses.Save(shared.Clone());

        var preview = LegacyImportService.Analyze(sourceStore.RootDirectory, _store);
        var report = LegacyImportService.Execute(preview, _store, new AppSettings(), new ImportOptions());

        Assert.Single(preview.NewExpenses);
        Assert.Equal(1, report.ExpensesImported);
        Assert.Equal(2, _expenses.LoadMonth(2026, 10).Count);
        Assert.NotNull(_expenses.GetAttachmentPath(_expenses.LoadMonth(2026, 10).Single(entry => entry.Id == withAttachment.Id)));

        using var archive = System.IO.Compression.ZipFile.OpenRead(report.BackupPath);
        Assert.Contains(archive.Entries, item => item.FullName == "NoteSpese/2026-10.json");
    }

    [Fact]
    public void Import_ForNewInstallation_TakesPreferencesAndEmployeeId()
    {
        var sourceStore = new PizziniStore(Path.Combine(_base, "Vecchia"));
        var sourceSettings = new AppSettings
        {
            EmployeeName = "Mario Rossi", AlwaysOnTop = true, LaunchAtStartup = true, KmRateCents = 42,
            CheckUpdatesAtStartup = false
        };
        sourceStore.SaveSettings(sourceSettings);
        var current = new AppSettings();

        var preview = LegacyImportService.Analyze(sourceStore.RootDirectory, _store, current);
        LegacyImportService.Execute(preview, _store, current, new ImportOptions(ImportSettings: true, ImportPreferences: true));
        var saved = _store.LoadSettings();

        Assert.Equal("Mario Rossi", saved.EmployeeName);
        Assert.Equal(sourceSettings.EmployeeId, saved.EmployeeId);
        Assert.True(saved.AlwaysOnTop);
        Assert.True(saved.LaunchAtStartup);
        Assert.False(saved.CheckUpdatesAtStartup);
        Assert.Equal(42, saved.KmRateCents);
    }

    [Fact]
    public void Analyze_DoesNotWriteMigratedSettingsInSourceFolder()
    {
        var sourceStore = new PizziniStore(Path.Combine(_base, "Vecchia"));
        const string legacy = "{\"schemaVersion\":2,\"employeeName\":\"Mario\",\"trackingStartDate\":\"2026-01-01\"}";
        File.WriteAllText(sourceStore.SettingsFilePath, legacy);

        LegacyImportService.Analyze(sourceStore.RootDirectory, _store);

        Assert.Equal(legacy, File.ReadAllText(sourceStore.SettingsFilePath));
    }

    [Fact]
    public void PreviousInstallationFinder_FindsSiblingFolderButNotCurrent()
    {
        var current = new PizziniStore(Path.Combine(_base, "PizziniMaker-1.3.0"));
        current.SaveSettings(new AppSettings { EmployeeName = "Nuovo" });
        var old = new PizziniStore(Path.Combine(_base, "PizziniMaker-1.2.0"));
        old.SaveSettings(new AppSettings { EmployeeName = "Mario Rossi" });
        var empty = new PizziniStore(Path.Combine(_base, "Altro"));

        var found = PreviousInstallationFinder.Find(current.RootDirectory, []);

        var item = Assert.Single(found);
        Assert.Equal(old.RootDirectory, item.RootDirectory);
        Assert.Equal("Mario Rossi", item.EmployeeName);
        Assert.NotNull(empty);
    }

    private static ExpenseEntry Meal(DateOnly date, long cents) =>
        new() { Date = date, Category = ExpenseCategory.Pasto, AmountCents = cents, Description = "Pranzo" };

    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }
}

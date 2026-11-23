using System.IO.Compression;
using PizziniMaker.Core;

namespace PizziniMaker.Tests;

public sealed class PrintAndUpdateTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "PizziniMakerUpdateTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Slip_ShowsHeaderLinesAndPaperBoxes()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.IsConfirmed = true;
        day.Entries =
        [
            new() { Type = ActivityType.Scheda, WorkCardNumber = "8092", WorkCardComment = "Baccane", Minutes = 270, IsCad = true, IsPaperRecorded = true },
            new() { Type = ActivityType.Mandato, WorkCardNumber = "123/X", Minutes = 60, IsCad = false },
            new() { Type = ActivityType.Viaggio, Minutes = 150 }
        ];

        var slip = PrintSlipBuilder.Build(day, "Mario Rossi");

        Assert.Equal("MARIO ROSSI", slip.EmployeeName);
        Assert.Equal("16/11/2026", slip.DateText);
        Assert.Equal(new PrintSlipLine("8092 Baccane — 4,5 ore (CAD)", true, true), slip.Lines[0]);
        Assert.Equal(new PrintSlipLine("MANDATO 123/X — 1 ora", true, false), slip.Lines[1]);
        Assert.Equal(new PrintSlipLine("VIAGGIO — 2,5 ore", false, false), slip.Lines[2]);
        Assert.StartsWith("Totale: 8 ore · viaggio 2,5 ore", slip.FooterText);
        Assert.DoesNotContain("BOZZA", slip.FooterText);
    }

    [Fact]
    public void Slip_MarksDrafts()
    {
        var day = WorkDay.CreateNew(new DateOnly(2026, 11, 16));
        day.Entries[0].WorkCardNumber = "1234";

        Assert.Contains("BOZZA", PrintSlipBuilder.Build(day, "x").FooterText);
    }

    [Fact]
    public void Paginate_FourSlipsPerA4SortedByDate()
    {
        var slips = Enumerable.Range(1, 9)
            .Select(index => new PrintSlip("X", new DateOnly(2026, 11, 10 - index + 10), [], "", true))
            .ToArray();

        var pages = PrintSlipBuilder.Paginate(slips);

        Assert.Equal(3, pages.Count);
        Assert.Equal([4, 4, 1], pages.Select(page => page.Count));
        Assert.True(pages[0][0].Date < pages[0][1].Date);
        Assert.Equal(3, PrintSlipBuilder.CountPages(9));
        Assert.Equal(0, PrintSlipBuilder.CountPages(0));
    }

    [Fact]
    public void Layout_IsA6OnA4Portrait()
    {
        Assert.Equal(105, SlipLayout.SlotWidthMm);
        Assert.Equal(148.5, SlipLayout.SlotHeightMm);
        Assert.Equal((105.0, 148.5), SlipLayout.SlotOrigin(3));
    }

    [Fact]
    public void TextWrapper_SplitsOnWordsAndLongWords()
    {
        static double Measure(string text) => text.Length;

        Assert.Equal(["uno due", "tre"], TextWrapper.Wrap("uno due tre", 7, Measure));
        Assert.Equal(["abcde", "fgh"], TextWrapper.Wrap("abcdefgh", 5, Measure));
        Assert.Equal([""], TextWrapper.Wrap("", 5, Measure));
    }

    [Theory]
    [InlineData("v1.3.0", "1.3.0")]
    [InlineData("1.3", "1.3.0")]
    [InlineData("V2.0.1", "2.0.1")]
    public void TryParseVersion_AcceptsTags(string tag, string expected)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Fact]
    public void IsNewer_ComparesThreeParts()
    {
        Assert.True(UpdateChecker.IsNewer(new Version(1, 3, 1), new Version(1, 3, 0, 0)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 3, 0), new Version(1, 3, 0, 0)));
        Assert.False(UpdateChecker.IsNewer(new Version(1, 2, 9), new Version(1, 3, 0)));
        Assert.False(UpdateChecker.TryParseVersion("ultima", out _));
    }

    [Fact]
    public void ParseRelease_FindsPackageAndChecksum()
    {
        const string json = """
            {
              "tag_name": "v1.4.0", "name": "PizziniMaker 1.4.0", "body": "Novità", "draft": false, "prerelease": false,
              "html_url": "https://github.com/iacorenx/PizziniMaker/releases/tag/v1.4.0",
              "assets": [
                { "name": "PizziniMaker-1.4.0-win-x64.zip", "size": 1000,
                  "browser_download_url": "https://github.com/iacorenx/PizziniMaker/releases/download/v1.4.0/PizziniMaker-1.4.0-win-x64.zip" },
                { "name": "PizziniMaker-1.4.0-win-x64.zip.sha256", "size": 90,
                  "browser_download_url": "https://github.com/iacorenx/PizziniMaker/releases/download/v1.4.0/PizziniMaker-1.4.0-win-x64.zip.sha256" },
                { "name": "insicuro.zip", "browser_download_url": "http://example.com/x.zip" }
              ]
            }
            """;

        var release = UpdateChecker.ParseRelease(json)!;

        Assert.Equal(new Version(1, 4, 0), release.Version);
        Assert.Equal("PizziniMaker-1.4.0-win-x64.zip", release.Package?.Name);
        Assert.Equal("PizziniMaker-1.4.0-win-x64.zip.sha256", release.Checksum?.Name);
        Assert.Equal("Novità", release.Notes);
    }

    [Fact]
    public void ParseRelease_IgnoresPrerelease() =>
        Assert.Null(UpdateChecker.ParseRelease("""{ "tag_name": "v9.0.0", "prerelease": true }"""));

    [Fact]
    public void ParseChecksum_AcceptsPowerShellAndSha256sumFormats()
    {
        var hash = new string('a', 64);
        Assert.Equal(hash.ToUpperInvariant(), UpdateChecker.ParseChecksum(hash));
        Assert.Equal(hash.ToUpperInvariant(), UpdateChecker.ParseChecksum($"{hash}  PizziniMaker-1.4.0-win-x64.zip\n"));
        Assert.Null(UpdateChecker.ParseChecksum("non valido"));
    }

    [Fact]
    public void Installer_ReplacesProgramButNeverData()
    {
        var app = Path.Combine(_base, "App");
        Directory.CreateDirectory(Path.Combine(app, "Dati"));
        File.WriteAllText(Path.Combine(app, "PizziniMaker.exe"), "vecchio");
        File.WriteAllText(Path.Combine(app, "Impostazioni.json"), "{dati}");
        File.WriteAllText(Path.Combine(app, "Dati", "2026-11-16.json"), "{giorno}");

        var package = Path.Combine(_base, "Pacchetto", "PizziniMaker");
        Directory.CreateDirectory(Path.Combine(package, "Dati"));
        File.WriteAllText(Path.Combine(package, "PizziniMaker.exe"), "nuovo");
        File.WriteAllText(Path.Combine(package, "GUIDA_UTENTE.md"), "guida");
        File.WriteAllText(Path.Combine(package, "Impostazioni.json"), "{vuote}");
        var zip = Path.Combine(_base, "pacchetto.zip");
        ZipFile.CreateFromDirectory(Path.Combine(_base, "Pacchetto"), zip);

        var extracted = UpdateInstaller.ExtractPackage(zip, Path.Combine(_base, "Staging"));
        UpdateInstaller.Apply(extracted, app);

        Assert.Equal("nuovo", File.ReadAllText(Path.Combine(app, "PizziniMaker.exe")));
        Assert.Equal("guida", File.ReadAllText(Path.Combine(app, "GUIDA_UTENTE.md")));
        Assert.Equal("{dati}", File.ReadAllText(Path.Combine(app, "Impostazioni.json")));
        Assert.Equal("{giorno}", File.ReadAllText(Path.Combine(app, "Dati", "2026-11-16.json")));
        Assert.True(File.Exists(Path.Combine(app, "PizziniMaker.exe.old")));

        Assert.Equal(1, UpdateInstaller.CleanupOldFiles(app));
        Assert.False(File.Exists(Path.Combine(app, "PizziniMaker.exe.old")));
    }

    [Fact]
    public void Installer_RejectsPackageWithoutExecutable()
    {
        var package = Path.Combine(_base, "Vuoto");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "leggimi.txt"), "x");
        var zip = Path.Combine(_base, "vuoto.zip");
        ZipFile.CreateFromDirectory(package, zip);

        Assert.Throws<InvalidDataException>(() => UpdateInstaller.ExtractPackage(zip, Path.Combine(_base, "Staging")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }
}

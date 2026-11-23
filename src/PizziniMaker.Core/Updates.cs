using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace PizziniMaker.Core;

public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size);

public sealed record ReleaseInfo(
    Version Version,
    string Tag,
    string Name,
    string Notes,
    Uri? PageUrl,
    ReleaseAsset? Package,
    ReleaseAsset? Checksum);

/// <summary>
/// Controllo delle nuove versioni pubblicate come release GitHub del repository pubblico,
/// senza token né dati personali nella richiesta.
/// </summary>
public static class UpdateChecker
{
    public const string Repository = "iacorenx/PizziniMaker";

    public static Uri LatestReleaseUrl { get; } = new($"https://api.github.com/repos/{Repository}/releases/latest");

    public static Uri ReleasesPageUrl { get; } = new($"https://github.com/{Repository}/releases");

    public static HttpClient CreateHttpClient(Version currentVersion)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PizziniMaker-Updater", Normalize(currentVersion).ToString()));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    public static async Task<ReleaseInfo?> GetLatestReleaseAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.GetAsync(LatestReleaseUrl, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null; // Nessuna release pubblicata.
        }
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseRelease(json);
    }

    /// <summary>Interpreta la risposta di <c>/releases/latest</c>; null per bozze, pre-release o tag non validi.</summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (GetBool(root, "draft") || GetBool(root, "prerelease"))
        {
            return null;
        }

        var tag = GetString(root, "tag_name");
        if (!TryParseVersion(tag, out var version))
        {
            return null;
        }

        var assets = new List<ReleaseAsset>();
        if (root.TryGetProperty("assets", out var assetArray) && assetArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetArray.EnumerateArray())
            {
                var name = GetString(asset, "name");
                var url = GetString(asset, "browser_download_url");
                if (name.Length > 0 && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    assets.Add(new ReleaseAsset(name, uri,
                        asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0));
                }
            }
        }

        var package = assets.FirstOrDefault(asset =>
            asset.Name.StartsWith("PizziniMaker-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase));
        var checksum = package is null ? null : assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, package.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
        Uri.TryCreate(GetString(root, "html_url"), UriKind.Absolute, out var page);

        return new ReleaseInfo(version, tag, GetString(root, "name"), GetString(root, "body"), page, package, checksum);
    }

    public static bool TryParseVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0);
        var value = (text ?? string.Empty).Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }
        if (!Version.TryParse(value, out var parsed) || parsed is null)
        {
            return false;
        }
        version = Normalize(parsed);
        return true;
    }

    public static Version Normalize(Version version) =>
        new(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0));

    public static bool IsNewer(Version candidate, Version current) => Normalize(candidate) > Normalize(current);

    /// <summary>Estrae l'hash da un file <c>.sha256</c> ("HASH" oppure "HASH  nomefile").</summary>
    public static string? ParseChecksum(string content)
    {
        var token = (content ?? string.Empty)
            .Split([' ', '\t', '\r', '\n', '*'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return token is { Length: 64 } && token.All(Uri.IsHexDigit) ? token.ToUpperInvariant() : null;
    }

    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static async Task DownloadAsync(HttpClient client, Uri url, string destination,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = File.Create(destination);
        var buffer = new byte[81920];
        long read = 0;
        int count;
        while ((count = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            read += count;
            if (total > 0)
            {
                progress?.Report((double)read / total.Value);
            }
        }
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

/// <summary>
/// Installa una nuova versione nella stessa cartella portabile: sostituisce solo i file del programma,
/// mai i dati (giornate, log, note spese, impostazioni, storico, backup).
/// </summary>
public static class UpdateInstaller
{
    public const string ExecutableName = "PizziniMaker.exe";
    public const string OldExecutableSuffix = ".old";

    /// <summary>Nomi di file e cartelle dei dati personali che un aggiornamento non tocca mai.</summary>
    public static IReadOnlySet<string> ProtectedNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Dati", "Log", "NoteSpese", "Backup", "Impostazioni.json", "Impostazioni.json.bak",
        "StoricoSchede.json", "StoricoSchede.json.bak"
    };

    /// <summary>Estrae lo ZIP in <paramref name="stagingDirectory"/> e restituisce la cartella che contiene l'eseguibile.</summary>
    public static string ExtractPackage(string zipPath, string stagingDirectory)
    {
        if (Directory.Exists(stagingDirectory))
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }
        Directory.CreateDirectory(stagingDirectory);
        ZipFile.ExtractToDirectory(zipPath, stagingDirectory);
        var executable = Directory.EnumerateFiles(stagingDirectory, ExecutableName, SearchOption.AllDirectories)
            .OrderBy(path => path.Length)
            .FirstOrDefault()
            ?? throw new InvalidDataException("Il pacchetto scaricato non contiene PizziniMaker.exe.");
        return Path.GetDirectoryName(executable)!;
    }

    /// <summary>
    /// Copia i file del pacchetto nella cartella dell'applicazione. L'eseguibile in uso viene prima rinominato
    /// (Windows lo consente anche mentre è in esecuzione). In caso di errore tutto viene ripristinato.
    /// Restituisce i file sostituiti o aggiunti.
    /// </summary>
    public static IReadOnlyList<string> Apply(string packageDirectory, string applicationDirectory)
    {
        var files = Directory.EnumerateFiles(packageDirectory)
            .Where(file => !ProtectedNames.Contains(Path.GetFileName(file)))
            .ToArray();
        if (!files.Any(file => string.Equals(Path.GetFileName(file), ExecutableName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Il pacchetto non contiene PizziniMaker.exe.");
        }

        var replaced = new List<(string Target, string? Previous)>();
        try
        {
            foreach (var file in files)
            {
                var target = Path.Combine(applicationDirectory, Path.GetFileName(file));
                string? previous = null;
                if (File.Exists(target))
                {
                    previous = target + OldExecutableSuffix;
                    if (File.Exists(previous))
                    {
                        File.Delete(previous);
                    }
                    File.Move(target, previous);
                }
                replaced.Add((target, previous));
                File.Copy(file, target);
            }
        }
        catch
        {
            foreach (var (target, previous) in Enumerable.Reverse(replaced))
            {
                TryRestore(target, previous);
            }
            throw;
        }

        return replaced.Select(item => item.Target).ToArray();
    }

    /// <summary>Elimina le copie <c>*.old</c> lasciate dall'aggiornamento precedente (chiamata all'avvio).</summary>
    public static int CleanupOldFiles(string applicationDirectory)
    {
        if (!Directory.Exists(applicationDirectory))
        {
            return 0;
        }

        var removed = 0;
        foreach (var file in Directory.EnumerateFiles(applicationDirectory, "*" + OldExecutableSuffix))
        {
            try
            {
                File.Delete(file);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Il vecchio eseguibile potrebbe essere ancora in chiusura: verrà rimosso al prossimo avvio.
            }
        }
        return removed;
    }

    private static void TryRestore(string target, string? previous)
    {
        try
        {
            if (File.Exists(target) && previous is not null && File.Exists(previous))
            {
                File.Delete(target);
            }
            else if (File.Exists(target) && previous is null)
            {
                File.Delete(target);
            }
            if (previous is not null && File.Exists(previous))
            {
                File.Move(previous, target);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Ripristino migliore possibile: il file .old resta comunque nella cartella.
        }
    }
}

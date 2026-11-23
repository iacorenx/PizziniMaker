namespace PizziniMaker.Core;

public sealed record PreviousInstallation(string RootDirectory, string EmployeeName, DateTime LastModified);

/// <summary>
/// Cerca le cartelle portabili di versioni precedenti: cartelle sorelle di quella attuale e
/// cartelle (o loro sottocartelle) dei percorsi indicati, ad esempio Desktop, Documenti e Download.
/// Viene solo letto il file delle impostazioni.
/// </summary>
public static class PreviousInstallationFinder
{
    public static IReadOnlyList<PreviousInstallation> Find(string currentRoot, IEnumerable<string> searchRoots)
    {
        ArgumentNullException.ThrowIfNull(searchRoots);
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentRoot));
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var roots = new List<string>(searchRoots);
        if (Path.GetDirectoryName(current) is { } parent)
        {
            roots.Insert(0, parent);
        }

        foreach (var root in roots.Where(Directory.Exists))
        {
            candidates.Add(root);
            foreach (var child in SafeEnumerateDirectories(root))
            {
                candidates.Add(child);
                foreach (var grandChild in SafeEnumerateDirectories(child))
                {
                    candidates.Add(grandChild);
                }
            }
        }

        var result = new List<PreviousInstallation>();
        foreach (var candidate in candidates.Select(Path.TrimEndingDirectorySeparator))
        {
            if (string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase) ||
                current.StartsWith(candidate + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                candidate.StartsWith(current + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var settingsPath = Path.Combine(candidate, "Impostazioni.json");
            if (!File.Exists(settingsPath) || !Directory.Exists(Path.Combine(candidate, "Dati")))
            {
                continue;
            }

            try
            {
                var settings = new PizziniStore(candidate, createDirectories: false).LoadSettings(saveMigration: false);
                if (!string.IsNullOrWhiteSpace(settings.EmployeeName))
                {
                    result.Add(new PreviousInstallation(candidate, settings.EmployeeName.Trim(),
                        LatestWrite(candidate)));
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                // Cartella non leggibile: ignorata.
            }
        }

        return result.OrderByDescending(item => item.LastModified).ToArray();
    }

    private static DateTime LatestWrite(string root)
    {
        var dates = new List<DateTime> { File.GetLastWriteTime(Path.Combine(root, "Impostazioni.json")) };
        try
        {
            dates.AddRange(Directory.EnumerateFiles(Path.Combine(root, "Dati")).Select(File.GetLastWriteTime));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
        return dates.Max();
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

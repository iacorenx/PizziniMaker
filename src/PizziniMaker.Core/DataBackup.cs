using System.Globalization;
using System.IO.Compression;

namespace PizziniMaker.Core;

/// <summary>Archivio ZIP dei dati personali (giornate, log, note spese, impostazioni, storico) in <c>Backup/</c>.</summary>
public static class DataBackup
{
    public static string Create(PizziniStore store, string prefix)
    {
        ArgumentNullException.ThrowIfNull(store);
        Directory.CreateDirectory(store.BackupDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var path = Path.Combine(store.BackupDirectory, $"{prefix}-{stamp}.zip");
        for (var suffix = 2; File.Exists(path); suffix++)
        {
            path = Path.Combine(store.BackupDirectory, $"{prefix}-{stamp}-{suffix}.zip");
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var directory in new[] { store.DataDirectory, store.LogDirectory, store.ExpenseDirectory })
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                archive.CreateEntryFromFile(file, Path.GetRelativePath(store.RootDirectory, file).Replace('\\', '/'));
            }
        }

        foreach (var file in new[] { store.SettingsFilePath, store.HistoryFilePath })
        {
            if (File.Exists(file))
            {
                archive.CreateEntryFromFile(file, Path.GetFileName(file));
            }
        }

        return path;
    }
}

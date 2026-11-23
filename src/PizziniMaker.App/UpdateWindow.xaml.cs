using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using PizziniMaker.Core;
using MessageBox = System.Windows.MessageBox;

namespace PizziniMaker.App;

/// <summary>Controllo, download e installazione della nuova versione da GitHub.</summary>
public partial class UpdateWindow : Window
{
    private readonly PizziniStore _store;
    private readonly Action _beforeInstall;
    private readonly Action _restart;
    private ReleaseInfo? _release;
    private bool _busy;

    public UpdateWindow(PizziniStore store, Action beforeInstall, Action restart, ReleaseInfo? knownRelease = null)
    {
        _store = store;
        _beforeInstall = beforeInstall;
        _restart = restart;
        _release = knownRelease;
        InitializeComponent();
        Title = $"Aggiornamenti - PizziniMaker {AppVersion.Display}";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_release is not null)
        {
            ShowRelease(_release);
            return;
        }
        await CheckAsync();
    }

    private async Task CheckAsync()
    {
        SetBusy(true, "Controllo delle versioni pubblicate su GitHub…");
        RetryButton.Visibility = Visibility.Collapsed;
        try
        {
            using var client = UpdateChecker.CreateHttpClient(AppVersion.Current);
            _release = await UpdateChecker.GetLatestReleaseAsync(client);
            if (_release is null)
            {
                StatusText.Text = $"Versione installata: {AppVersion.Display}.\nNon risultano versioni pubblicate.";
            }
            else
            {
                ShowRelease(_release);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            StatusText.Text = $"Versione installata: {AppVersion.Display}.\n" +
                $"Impossibile contattare GitHub: {exception.Message}\nVerificare la connessione a internet e riprovare.";
            RetryButton.Visibility = Visibility.Visible;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowRelease(ReleaseInfo release)
    {
        var newer = UpdateChecker.IsNewer(release.Version, AppVersion.Current);
        HeaderText.Text = newer ? $"Disponibile la versione {release.Version.ToString(3)}" : "PizziniMaker è aggiornato";
        StatusText.Text = newer
            ? $"Versione installata: {AppVersion.Display}. Nuova versione: {release.Version.ToString(3)}.\n" +
              "L'aggiornamento sostituisce solo il programma: giornate, log, note spese, impostazioni e storico restano nella cartella. " +
              "Prima dell'installazione viene creato un backup ZIP dei dati in Backup."
            : $"Versione installata: {AppVersion.Display} (ultima pubblicata: {release.Version.ToString(3)}).";
        if (newer && !string.IsNullOrWhiteSpace(release.Notes))
        {
            NotesText.Text = release.Notes.Trim();
            NotesBorder.Visibility = Visibility.Visible;
        }

        var canInstall = newer && release.Package is not null && release.Checksum is not null;
        InstallButton.Visibility = canInstall ? Visibility.Visible : Visibility.Collapsed;
        if (newer && !canInstall)
        {
            StatusText.Text += "\nLa versione pubblicata non contiene il pacchetto verificabile (ZIP e .sha256): scaricarla dalla pagina delle versioni.";
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_release?.Package is not { } package || _release.Checksum is not { } checksumAsset)
        {
            return;
        }

        var answer = MessageBox.Show(this,
            $"Installare PizziniMaker {_release.Version.ToString(3)}? Al termine il programma verrà riavviato.",
            "Aggiornamento", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var workDirectory = Path.Combine(Path.GetTempPath(), "PizziniMaker-update", _release.Version.ToString(3));
        var zipPath = Path.Combine(workDirectory, package.Name);
        try
        {
            SetBusy(true, "Download in corso…");
            Progress.Visibility = Visibility.Visible;
            using (var client = UpdateChecker.CreateHttpClient(AppVersion.Current))
            {
                var checksumPath = zipPath + ".sha256";
                await UpdateChecker.DownloadAsync(client, checksumAsset.DownloadUrl, checksumPath);
                await UpdateChecker.DownloadAsync(client, package.DownloadUrl, zipPath,
                    new Progress<double>(value => Progress.Value = value));
                var expected = UpdateChecker.ParseChecksum(await File.ReadAllTextAsync(checksumPath))
                    ?? throw new InvalidDataException("Il file .sha256 della versione non è valido.");
                StatusText.Text = "Verifica dell'integrità del pacchetto…";
                var actual = await Task.Run(() => UpdateChecker.ComputeSha256(zipPath));
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("Il pacchetto scaricato non corrisponde all'impronta SHA-256 pubblicata: installazione annullata.");
                }
            }

            StatusText.Text = "Preparazione dei file…";
            var packageDirectory = await Task.Run(() => UpdateInstaller.ExtractPackage(zipPath, Path.Combine(workDirectory, "pacchetto")));

            StatusText.Text = "Backup dei dati…";
            _beforeInstall();
            var backup = await Task.Run(() => DataBackup.Create(_store, "Pre-aggiornamento"));

            StatusText.Text = "Installazione…";
            UpdateInstaller.Apply(packageDirectory, AppContext.BaseDirectory);
            StatusText.Text = $"Installazione completata. Backup dei dati: {backup}\nRiavvio in corso…";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException
                                              or UnauthorizedAccessException or InvalidDataException)
        {
            SetBusy(false);
            Progress.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Aggiornamento non riuscito: {exception.Message}\nLa versione installata non è stata modificata.";
            if (exception is UnauthorizedAccessException)
            {
                StatusText.Text += "\nLa cartella del programma non è scrivibile: spostarla in una cartella dell'utente (es. Documenti).";
            }
            RetryButton.Visibility = Visibility.Visible;
            return;
        }

        _busy = false;
        _restart();
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        if (message is not null)
        {
            StatusText.Text = message;
        }
        InstallButton.IsEnabled = !busy;
        RetryButton.IsEnabled = !busy;
        CloseButton.IsEnabled = !busy;
    }

    private async void Retry_Click(object sender, RoutedEventArgs e) => await CheckAsync();

    private void ReleasePage_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo((_release?.PageUrl ?? UpdateChecker.ReleasesPageUrl).ToString()) { UseShellExecute = true });

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e) => e.Cancel = _busy;
}

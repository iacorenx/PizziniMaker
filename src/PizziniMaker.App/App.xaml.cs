using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Forms;
using PizziniMaker.Core;
using WpfApplication = System.Windows.Application;

namespace PizziniMaker.App;

public partial class App : WpfApplication, ISettingsActions
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private PizziniStore _store = null!;
    private AppSettings _settings = null!;
    private MainViewModel _viewModel = null!;
    private MainWindow _mainWindow = null!;
    private NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _applicationIcon;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var afterUpdate = e.Args.Contains(AfterUpdateArgument, StringComparer.OrdinalIgnoreCase);
        _singleInstanceMutex = new Mutex(true, @"Local\PizziniMaker.Portable", out _ownsMutex);
        if (!_ownsMutex && afterUpdate)
        {
            // Dopo un aggiornamento la versione precedente sta ancora chiudendosi: la si attende.
            try
            {
                _ownsMutex = _singleInstanceMutex.WaitOne(TimeSpan.FromSeconds(20));
            }
            catch (AbandonedMutexException)
            {
                _ownsMutex = true;
            }
        }
        if (!_ownsMutex)
        {
            System.Windows.MessageBox.Show("PizziniMaker è già in esecuzione. Usa la sua icona accanto all'orologio.",
                "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var italian = CultureInfo.GetCultureInfo("it-IT");
        CultureInfo.DefaultThreadCurrentCulture = italian;
        CultureInfo.DefaultThreadCurrentUICulture = italian;

        UpdateInstaller.CleanupOldFiles(AppContext.BaseDirectory);
        _store = new PizziniStore(AppContext.BaseDirectory);
        try
        {
            _settings = _store.LoadSettings();
        }
        catch (InvalidDataException exception)
        {
            System.Windows.MessageBox.Show($"Impostazioni non leggibili: {exception.Message}\nVerranno usate le impostazioni predefinite.",
                "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Warning);
            _settings = new AppSettings();
        }

        if (string.IsNullOrWhiteSpace(_settings.EmployeeName))
        {
            OfferImportFromPreviousInstallation();
        }

        if (string.IsNullOrWhiteSpace(_settings.EmployeeName))
        {
            var setupWindow = new SettingsWindow(_settings, true, this)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            if (setupWindow.ShowDialog() != true || !SaveSettings(null))
            {
                Shutdown();
                return;
            }
        }
        else if (_settings.LaunchAtStartup)
        {
            try
            {
                StartupService.SetLaunchAtStartup(true);
            }
            catch (Exception exception)
            {
                System.Windows.MessageBox.Show($"Avvio automatico non aggiornato: {exception.Message}",
                    "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        _viewModel = new MainViewModel(_store, _settings);
        _mainWindow = new MainWindow(_viewModel)
        {
            Topmost = _settings.AlwaysOnTop
        };
        _mainWindow.Show();
        InitializeTrayIcon();

        if (afterUpdate)
        {
            _notifyIcon?.ShowBalloonTip(6000, "PizziniMaker",
                $"Aggiornato alla versione {AppVersion.Display}. Dati e impostazioni sono stati mantenuti.", ToolTipIcon.Info);
        }
        else if (_viewModel.MissingDaysCount > 0)
        {
            _notifyIcon?.ShowBalloonTip(6000, "PizziniMaker",
                _viewModel.MissingDaysText, ToolTipIcon.Warning);
        }

        _ = CheckForUpdatesInBackgroundAsync();
    }

    public const string AfterUpdateArgument = "--dopo-aggiornamento";

    /// <summary>Nuova installazione in un'altra cartella: propone di importare dati e preferenze della versione precedente.</summary>
    private void OfferImportFromPreviousInstallation()
    {
        IReadOnlyList<PreviousInstallation> found;
        try
        {
            var places = new[]
                {
                    Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments, Environment.SpecialFolder.UserProfile
                }
                .Select(Environment.GetFolderPath)
                .Append(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"))
                .Where(path => !string.IsNullOrEmpty(path));
            found = PreviousInstallationFinder.Find(_store.RootDirectory, places);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var previous = found.FirstOrDefault();
        var message = previous is null
            ? "Benvenuto in PizziniMaker!\n\nHai già usato una versione precedente su questo PC? " +
              "Puoi importarne giornate, log, note spese, storico, nome e preferenze scegliendo la sua cartella."
            : $"È stata trovata una versione precedente di PizziniMaker:\n{previous.RootDirectory}\n" +
              $"Dipendente: {previous.EmployeeName} · ultimo utilizzo {previous.LastModified:dd/MM/yyyy}\n\n" +
              "Importare giornate, log, note spese, storico, nome e preferenze? La cartella precedente viene solo letta.";
        var answer = System.Windows.MessageBox.Show(message, "PizziniMaker - nuova installazione",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            ImportLegacyData(null, previous?.RootDirectory, newInstallation: true);
        }
    }

    public void ShowSettings(Window owner)
    {
        _viewModel.SaveCurrentDay();
        var settingsWindow = new SettingsWindow(_settings, false, this) { Owner = owner };
        if (settingsWindow.ShowDialog() != true || !SaveSettings(owner))
        {
            return;
        }

        _viewModel.ApplySettings(_settings);
        _mainWindow.Topmost = _settings.AlwaysOnTop;
    }

    public void ManageWorkCardHistory(Window owner)
    {
        _viewModel?.SaveCurrentDay();
        WorkCardHistory history;
        try
        {
            history = _store.LoadOrRebuildHistory();
        }
        catch (Exception exception)
        {
            ShowError(owner, $"Impossibile leggere lo storico schede: {exception.Message}");
            return;
        }

        var window = new WorkCardHistoryWindow(history) { Owner = owner };
        if (window.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _store.SaveHistory(window.Result);
        }
        catch (Exception exception)
        {
            ShowError(owner, $"Impossibile salvare lo storico schede: {exception.Message}");
            return;
        }

        _viewModel?.ReloadHistory();
    }

    public bool ImportLegacyData(Window owner) => ImportLegacyData(owner, null, newInstallation: false);

    private bool ImportLegacyData(Window? owner, string? sourceFolder, bool newInstallation)
    {
        _viewModel?.SaveCurrentDay();
        if (sourceFolder is null)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Seleziona la cartella PizziniMaker della versione precedente (quella che contiene Dati, Log e Impostazioni.json)",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
                InitialDirectory = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(_store.RootDirectory)) ?? string.Empty
            };
            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return false;
            }
            sourceFolder = dialog.SelectedPath;
        }

        ImportPreview preview;
        try
        {
            preview = LegacyImportService.Analyze(sourceFolder, _store, _settings);
        }
        catch (InvalidOperationException exception)
        {
            ShowError(owner, exception.Message);
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ShowError(owner, $"Impossibile leggere la cartella selezionata: {exception.Message}");
            return false;
        }

        var previewWindow = new ImportWindow(preview, newInstallation) { Owner = owner };
        if (owner is null)
        {
            previewWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        if (previewWindow.ShowDialog() != true)
        {
            return false;
        }

        // Su una cartella nuova (nessun nome) vengono riprese anche identità e preferenze.
        var importPreferences = previewWindow.ImportSettings &&
            (newInstallation || string.IsNullOrWhiteSpace(_settings.EmployeeName));
        ImportReport report;
        try
        {
            report = LegacyImportService.Execute(preview, _store, _settings,
                new ImportOptions(previewWindow.ImportSettings, importPreferences));
            if (importPreferences && _settings.LaunchAtStartup)
            {
                StartupService.SetLaunchAtStartup(true);
            }
        }
        catch (Exception exception)
        {
            ShowError(owner, $"Importazione non completata: {exception.Message}\n\n" +
                $"I dati presenti prima dell'importazione sono salvati nella cartella {_store.BackupDirectory}.");
            _viewModel?.ReloadFromStore();
            return false;
        }

        if (_viewModel is not null)
        {
            _viewModel.ApplySettings(_settings);
            _viewModel.ReloadFromStore();
        }

        new ImportWindow(report)
        {
            Owner = owner,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
        }.ShowDialog();
        return report.CustomActivitiesAdded > 0 || report.SettingsUpdated;
    }

    private static void ShowError(Window? owner, string message)
    {
        if (owner is null)
        {
            System.Windows.MessageBox.Show(message, "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            System.Windows.MessageBox.Show(owner, message, "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void ShowPrintWindow(Window? owner)
    {
        _viewModel.SaveCurrentDay();
        var window = new PrintWindow(_store, () => _settings.EmployeeName, _viewModel.CurrentDate);
        if (owner is { IsVisible: true })
        {
            window.Owner = owner;
        }
        else
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        window.ShowDialog();
    }

    private UpdateWindow? _updateWindow;

    public void ShowUpdateWindow(ReleaseInfo? knownRelease = null)
    {
        if (_updateWindow is not null)
        {
            _updateWindow.Activate();
            return;
        }

        _updateWindow = new UpdateWindow(_store, () => _viewModel.SaveCurrentDay(), RestartAfterUpdate, knownRelease)
        {
            Topmost = _settings.AlwaysOnTop
        };
        _updateWindow.Closed += (_, _) => _updateWindow = null;
        _updateWindow.Show();
        _updateWindow.Activate();
    }

    /// <summary>Avvia la nuova versione appena installata e chiude quella in esecuzione.</summary>
    private void RestartAfterUpdate()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, UpdateInstaller.ExecutableName);
        if (_ownsMutex && _singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _ownsMutex = false;
        }
        try
        {
            Process.Start(new ProcessStartInfo(executable, AfterUpdateArgument)
            {
                UseShellExecute = false,
                WorkingDirectory = AppContext.BaseDirectory
            });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException)
        {
            System.Windows.MessageBox.Show($"Aggiornamento installato, ma il riavvio automatico non è riuscito: {exception.Message}\n" +
                "Riaprire PizziniMaker manualmente.", "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        ExitApplication();
    }

    private ReleaseInfo? _availableRelease;

    /// <summary>Controllo silenzioso, al massimo una volta al giorno: avvisa solo con un fumetto nella tray.</summary>
    private async Task CheckForUpdatesInBackgroundAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (!_settings.CheckUpdatesAtStartup || _settings.LastUpdateCheck == today)
        {
            return;
        }

        try
        {
            using var client = UpdateChecker.CreateHttpClient(AppVersion.Current);
            var release = await UpdateChecker.GetLatestReleaseAsync(client);
            _settings.LastUpdateCheck = today;
            _store.SaveSettings(_settings);
            if (release is not null && UpdateChecker.IsNewer(release.Version, AppVersion.Current))
            {
                _availableRelease = release;
                _notifyIcon?.ShowBalloonTip(8000, "PizziniMaker",
                    $"È disponibile la versione {release.Version.ToString(3)}. Clic qui o tasto destro sull'icona → Verifica aggiornamenti.",
                    ToolTipIcon.Info);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            // Nessuna connessione: si riproverà al prossimo avvio.
        }
    }

    public void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = _settings.AlwaysOnTop;
    }

    public void OpenLogFolder()
    {
        Directory.CreateDirectory(_store.LogDirectory);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_store.LogDirectory}\"")
        {
            UseShellExecute = true
        });
    }

    public void ExitApplication()
    {
        IsExiting = true;
        _viewModel.SaveCurrentDay();
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }
        _mainWindow.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        if (_ownsMutex && _singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
        }
        _singleInstanceMutex?.Dispose();
        _applicationIcon?.Dispose();
        base.OnExit(e);
    }

    private bool SaveSettings(Window? owner)
    {
        try
        {
            StartupService.SetLaunchAtStartup(_settings.LaunchAtStartup);
            _store.SaveSettings(_settings);
            return true;
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(owner, $"Impossibile salvare le impostazioni: {exception.Message}",
                "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void InitializeTrayIcon()
    {
        var iconResource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/PizziniMaker.ico"))
            ?? throw new InvalidOperationException("Icona dell'applicazione non trovata.");
        using (iconResource.Stream)
        {
            _applicationIcon = new System.Drawing.Icon(iconResource.Stream, 32, 32);
        }

        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri PizziniMaker", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add("Stampa giorni…", null, (_, _) => Dispatcher.Invoke(() => ShowPrintWindow(_mainWindow)));
        menu.Items.Add("Apri cartella log", null, (_, _) => Dispatcher.Invoke(OpenLogFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Verifica aggiornamenti…", null, (_, _) => Dispatcher.Invoke(() => ShowUpdateWindow(_availableRelease)));
        menu.Items.Add(new ToolStripMenuItem($"PizziniMaker {AppVersion.Display} · {AppVersion.Credits}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _notifyIcon = new NotifyIcon
        {
            Icon = _applicationIcon,
            Text = $"PizziniMaker {AppVersion.Display} - Registro ore",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        _notifyIcon.BalloonTipClicked += (_, _) => Dispatcher.Invoke(() =>
        {
            if (_availableRelease is not null)
            {
                ShowUpdateWindow(_availableRelease);
            }
            else
            {
                ShowMainWindow();
            }
        });
    }
}

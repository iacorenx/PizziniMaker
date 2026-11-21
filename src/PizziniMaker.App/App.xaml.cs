using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        _singleInstanceMutex = new Mutex(true, @"Local\PizziniMaker.Portable", out _ownsMutex);
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

        _store = new PizziniStore(AppContext.BaseDirectory);
        _settings = _store.LoadSettings();

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

        if (_viewModel.MissingDaysCount > 0)
        {
            _notifyIcon?.ShowBalloonTip(6000, "PizziniMaker",
                _viewModel.MissingDaysText, ToolTipIcon.Warning);
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

    public bool ImportLegacyData(Window owner)
    {
        _viewModel?.SaveCurrentDay();
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

        ImportPreview preview;
        try
        {
            preview = LegacyImportService.Analyze(dialog.SelectedPath, _store, _settings);
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

        var previewWindow = new ImportWindow(preview) { Owner = owner };
        if (previewWindow.ShowDialog() != true)
        {
            return false;
        }

        ImportReport report;
        try
        {
            report = LegacyImportService.Execute(preview, _store, _settings, new ImportOptions(previewWindow.ImportSettings));
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

        new ImportWindow(report) { Owner = owner }.ShowDialog();
        return report.CustomActivitiesAdded > 0 || report.SettingsUpdated;
    }

    private static void ShowError(Window owner, string message) =>
        System.Windows.MessageBox.Show(owner, message, "PizziniMaker", MessageBoxButton.OK, MessageBoxImage.Warning);

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
        menu.Items.Add("Apri cartella log", null, (_, _) => Dispatcher.Invoke(OpenLogFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Energy Srl · Iacopo Preci · 2026") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _notifyIcon = new NotifyIcon
        {
            Icon = _applicationIcon,
            Text = "PizziniMaker - Registro ore",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }
}

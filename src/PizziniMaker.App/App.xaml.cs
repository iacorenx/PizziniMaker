using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using PizziniMaker.Core;
using WpfApplication = System.Windows.Application;

namespace PizziniMaker.App;

public partial class App : WpfApplication
{
    private Mutex? _singleInstanceMutex;
    private bool _ownsMutex;
    private PizziniStore _store = null!;
    private AppSettings _settings = null!;
    private MainViewModel _viewModel = null!;
    private MainWindow _mainWindow = null!;
    private NotifyIcon? _notifyIcon;

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
            var setupWindow = new SettingsWindow(_settings, true)
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
        var settingsWindow = new SettingsWindow(_settings, false) { Owner = owner };
        if (settingsWindow.ShowDialog() != true || !SaveSettings(owner))
        {
            return;
        }

        _viewModel.ApplySettings(_settings);
        _mainWindow.Topmost = _settings.AlwaysOnTop;
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
        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri PizziniMaker", null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        menu.Items.Add("Apri cartella log", null, (_, _) => Dispatcher.Invoke(OpenLogFolder));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "PizziniMaker - Registro ore",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }
}

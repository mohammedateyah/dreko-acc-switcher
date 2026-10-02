using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using DrekoAccSwitcher.Services;
using Forms = System.Windows.Forms;
using Localization = DrekoAccSwitcher.Services.Localization;

namespace DrekoAccSwitcher;

public partial class App : System.Windows.Application
{
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _openMenuItem;
    private Forms.ToolStripMenuItem? _exitMenuItem;
    private Mutex? _instanceMutex;
    private bool _allowExit;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            SettingsStore.Load();
        }
        catch (Exception ex)
        {
            Log.Write($"Settings load failed: {ex}");
            MessageBox.Show(
                $"Could not load application settings: {ex.Message}",
                "Dreko Acc Switcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        Localization.ApplyCulture();

        _instanceMutex = new Mutex(
            initiallyOwned: false,
            name: @"Local\DrekoAccSwitcher.SingleInstance");
        bool isFirstInstance;
        try
        {
            isFirstInstance = _instanceMutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            isFirstInstance = true;
        }
        if (!isFirstInstance)
        {
            _instanceMutex.Dispose();
            _instanceMutex = null;
            MessageBox.Show(
                Localization.Text("alreadyRunning"),
                Localization.Text("appTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information,
                MessageBoxResult.OK,
                Localization.IsArabic
                    ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                    : MessageBoxOptions.None);
            Shutdown();
            return;
        }

        CreateTrayIcon();

        MainWindow = new MainWindow();
        MainWindow.Show();
        _trayIcon!.Visible = false;
    }

    public bool TryHideToTray(CancelEventArgs e)
    {
        if (_allowExit || !SettingsStore.Current.CloseToTray)
            return false;

        e.Cancel = true;
        MainWindow.Hide();
        _trayIcon!.Visible = true;
        return true;
    }

    public void ShowMainWindow()
    {
        _trayIcon!.Visible = false;
        MainWindow.Show();
        MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
        MainWindow.Topmost = true;
        MainWindow.Topmost = false;
    }

    public void UpdateTrayLanguage()
    {
        if (_openMenuItem is not null)
            _openMenuItem.Text = Localization.Text("open");
        if (_exitMenuItem is not null)
            _exitMenuItem.Text = Localization.Text("exit");
        if (_trayIcon is not null)
            _trayIcon.Text = Localization.Text("appTitle");
    }

    private void CreateTrayIcon()
    {
        var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/drekoaccswitcher.png", UriKind.Absolute))
                      ?? throw new FileNotFoundException("The application tray icon resource was not found.");
        using var bitmap = new Bitmap(resource.Stream);
        var handle = bitmap.GetHicon();
        Icon icon;
        try
        {
            icon = (Icon)Icon.FromHandle(handle).Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }

        var menu = new Forms.ContextMenuStrip();
        _openMenuItem = new Forms.ToolStripMenuItem();
        _openMenuItem.Click += (_, _) => ShowMainWindow();
        _exitMenuItem = new Forms.ToolStripMenuItem();
        _exitMenuItem.Click += (_, _) =>
        {
            _allowExit = true;
            _trayIcon!.Visible = false;
            MainWindow.Close();
            Shutdown();
        };
        menu.Items.Add(_openMenuItem);
        menu.Items.Add(_exitMenuItem);
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Visible = false,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
        UpdateTrayLanguage();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon.Icon?.Dispose();
        }
        if (_instanceMutex is not null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }
        base.OnExit(e);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DrekoAccSwitcher.Services;
using DrekoAccSwitcher.ViewModels;
using Localization = DrekoAccSwitcher.Services.Localization;

namespace DrekoAccSwitcher;

public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 2;

    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        SourceInitialized += MainWindow_SourceInitialized;
        Closing += MainWindow_Closing;
        StateChanged += MainWindow_StateChanged;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.SearchBoxVisibility) &&
                viewModel.SearchVisible)
                Dispatcher.BeginInvoke(SearchBox.Focus);
        };
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WindowProcedure);
    }

    private static IntPtr WindowProcedure(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmGetMinMaxInfo)
            return IntPtr.Zero;

        var monitor = MonitorFromWindow(windowHandle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return IntPtr.Zero;

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return IntPtr.Zero;

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMaxInfo.MaxPosition.X = monitorInfo.Work.Left - monitorInfo.Monitor.Left;
        minMaxInfo.MaxPosition.Y = monitorInfo.Work.Top - monitorInfo.Monitor.Top;
        minMaxInfo.MaxSize.X = monitorInfo.Work.Right - monitorInfo.Work.Left;
        minMaxInfo.MaxSize.Y = monitorInfo.Work.Bottom - monitorInfo.Work.Top;
        Marshal.StructureToPtr(minMaxInfo, lParam, false);
        handled = true;
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rectangle Monitor;
        public Rectangle Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr windowHandle, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitorHandle, ref MonitorInfo monitorInfo);

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        MaximizeGlyph.Visibility = WindowState == WindowState.Maximized
            ? Visibility.Collapsed
            : Visibility.Visible;
        RestoreGlyph.Visibility = WindowState == WindowState.Maximized
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e) =>
        ((App)Application.Current).TryHideToTray(e);

    private void Minimize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeRestore_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void DrekoStudio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://dreko8u.web.app")
            {
                UseShellExecute = true
            });
        }
        catch (Win32Exception ex)
        {
            Log.Write($"Could not open DrekoStudio website: {ex}");
            MessageBox.Show(
                ex.Message,
                "DrekoStudio",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (InvalidOperationException ex)
        {
            Log.Write($"Could not open DrekoStudio website: {ex}");
            MessageBox.Show(
                ex.Message,
                "DrekoStudio",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Views.SettingsWindow { Owner = this };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            Log.Write($"Settings window failed: {ex}");
            MessageBox.Show(
                Localization.Format("saveSettingsFailed", ex.Message),
                Localization.Text("settingsTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

}

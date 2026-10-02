using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using DrekoAccSwitcher.Models;
using DrekoAccSwitcher.Services;
using Localization = DrekoAccSwitcher.Services.Localization;

namespace DrekoAccSwitcher.Views;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = this;
        FlowDirection = Localization.FlowDirection;
        EnglishOption.IsChecked = SettingsStore.Current.Language == "en";
        ArabicOption.IsChecked = SettingsStore.Current.Language == "ar";
        StartupBox.IsChecked = SettingsStore.Current.LaunchOnStartup;
        TrayBox.IsChecked = SettingsStore.Current.CloseToTray;
    }

    public string SettingsTitle => Localization.Text("settingsTitle");
    public string LanguageLabel => Localization.Text("language");
    public string StartupLabel => Localization.Text("launchOnStartup");
    public string TrayLabel => Localization.Text("closeToTray");
    public string TrayHint => Localization.Text("trayHint");
    public string CancelLabel => Localization.Text("cancel");
    public string SaveLabel => Localization.Text("save");
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SettingsStore.Save(new AppSettings
            {
                Language = ArabicOption.IsChecked == true ? "ar" : "en",
                LaunchOnStartup = StartupBox.IsChecked == true,
                CloseToTray = TrayBox.IsChecked == true
            });
            Localization.ApplyCulture();
            Localization.NotifyChanged();
            ((App)Application.Current).UpdateTrayLanguage();
            FlowDirection = Localization.FlowDirection;
            RaiseLocalizedProperties();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                Localization.Format("saveSettingsFailed", ex.Message),
                Localization.Text("settingsTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning,
                MessageBoxResult.OK,
                Localization.IsArabic
                    ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign
                    : MessageBoxOptions.None);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void RaiseLocalizedProperties()
    {
        Raise(nameof(SettingsTitle));
        Raise(nameof(LanguageLabel));
        Raise(nameof(StartupLabel));
        Raise(nameof(TrayLabel));
        Raise(nameof(TrayHint));
        Raise(nameof(CancelLabel));
        Raise(nameof(SaveLabel));
    }

    private void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

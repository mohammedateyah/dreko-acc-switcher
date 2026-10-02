namespace DrekoAccSwitcher.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public bool LaunchOnStartup { get; set; }
    public bool CloseToTray { get; set; }
    public bool WelcomeMessageShown { get; set; } = true;
}

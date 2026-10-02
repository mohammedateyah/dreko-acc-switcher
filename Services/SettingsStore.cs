using System.Text.Json;
using DrekoAccSwitcher.Models;
using Microsoft.Win32;

namespace DrekoAccSwitcher.Services;

public static class SettingsStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DrekoAccSwitcher";

    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        AppPaths.EnsureLayout();
        if (!File.Exists(AppPaths.SettingsFile))
        {
            Current = new AppSettings();
            return;
        }

        Current = JsonSerializer.Deserialize<AppSettings>(
                      File.ReadAllText(AppPaths.SettingsFile), JsonUtil.Options)
                  ?? throw new InvalidDataException("The application settings file contains no data.");

        if (Current.Language is not ("en" or "ar"))
            Current.Language = "en";
    }

    public static void Save(AppSettings settings)
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "DrekoAccSwitcher.exe");
        if (settings.LaunchOnStartup && !File.Exists(executablePath))
            throw new FileNotFoundException("The application executable needed for Windows startup was not found.", executablePath);

        var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (runKey is null && settings.LaunchOnStartup)
            runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath)
                     ?? throw new InvalidOperationException("Could not create the Windows startup registry key.");

        using (runKey)
        {
            if (settings.LaunchOnStartup)
                runKey!.SetValue(RunValueName, $"\"{executablePath}\"");
            else
                runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);
        }

        var temporaryFile = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(settings, JsonUtil.Options));
        File.Move(temporaryFile, AppPaths.SettingsFile, overwrite: true);
        Current = settings;
    }
}

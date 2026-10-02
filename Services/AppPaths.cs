using DrekoAccSwitcher.Models;

namespace DrekoAccSwitcher.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Dreko Acc Switcher");

    public static string CatalogFile => Path.Combine(Root, "accounts.json");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string LogFile => Path.Combine(Root, "dreko.log");
    public static string LoginCache => Path.Combine(Root, "LoginCache");

    public static string AccountFolder(string platformId, string accountId) =>
        Path.Combine(LoginCache, Sanitize(platformId), Sanitize(accountId));

    public static void EnsureLayout()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LoginCache);
    }

    public static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Trim();
    }
}

public static class Log
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            AppPaths.EnsureLayout();
            lock (Gate)
            {
                File.AppendAllText(AppPaths.LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging must never break switching
        }
    }
}

public static class PathExpander
{
    public static string Expand(string path)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var steam = SteamLocator.FindSteamRoot() ?? Path.Combine(pfx86, "Steam");

        return Environment.ExpandEnvironmentVariables(path)
            .Replace("{LocalAppData}", local, StringComparison.OrdinalIgnoreCase)
            .Replace("{AppData}", roaming, StringComparison.OrdinalIgnoreCase)
            .Replace("{ProgramFiles}", pf, StringComparison.OrdinalIgnoreCase)
            .Replace("{ProgramFilesX86}", pfx86, StringComparison.OrdinalIgnoreCase)
            .Replace("{Steam}", steam, StringComparison.OrdinalIgnoreCase)
            .Replace("%LOCALAPPDATA%", local, StringComparison.OrdinalIgnoreCase)
            .Replace("%APPDATA%", roaming, StringComparison.OrdinalIgnoreCase);
    }
}

public static class SteamLocator
{
    public static string? FindSteamRoot()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            var path = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                return path.Replace('/', Path.DirectorySeparatorChar);
        }
        catch
        {
            // ignore
        }

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
        return Directory.Exists(fallback) ? fallback : null;
    }

    public static string? FindSteamExe()
    {
        var root = FindSteamRoot();
        if (root is null) return null;
        var exe = Path.Combine(root, "steam.exe");
        return File.Exists(exe) ? exe : null;
    }
}

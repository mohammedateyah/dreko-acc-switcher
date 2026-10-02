using System.Diagnostics;
using System.Text.RegularExpressions;
using DrekoAccSwitcher.Models;
using Microsoft.Win32;

namespace DrekoAccSwitcher.Services;

public static class ProcessHelper
{
    public static async Task CloseAsync(
        IEnumerable<string> processNames,
        string? exePath,
        string? shutdownArgs,
        CancellationToken ct = default)
    {
        var names = processNames
            .Select(n => n.Replace(".exe", "", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath) && !string.IsNullOrWhiteSpace(shutdownArgs))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = shutdownArgs,
                    UseShellExecute = true
                });
                await Task.Delay(1500, ct);
            }
            catch (Exception ex)
            {
                Log.Write($"Shutdown launch failed: {ex.Message}");
            }
        }

        foreach (var name in names)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!proc.HasExited)
                        proc.CloseMainWindow();
                }
                catch
                {
                    // continue
                }
            }
        }

        await Task.Delay(800, ct);

        foreach (var name in names)
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    Log.Write($"Kill {name} failed: {ex.Message}");
                }
            }
        }

        await Task.Delay(400, ct);
    }

    public static void Launch(string exePath, string? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            throw new FileNotFoundException("Launcher executable was not found.", exePath);

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments ?? "",
            WorkingDirectory = Path.GetDirectoryName(exePath),
            UseShellExecute = true
        });
    }
}

public static class LauncherLocator
{
    private static readonly object DiscoveryGate = new();
    private static readonly Dictionary<string, string> DiscoveredExecutables =
        new(StringComparer.OrdinalIgnoreCase);
    private static Task? _discoveryTask;

    public static string? FindExe(Models.PlatformDefinition platform)
    {
        var executableNames = platform.ExeCandidates
            .Select(candidate => Path.GetFileName(PathExpander.Expand(candidate)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var candidate in platform.ExeCandidates)
        {
            var path = PathExpander.Expand(candidate);
            if (File.Exists(path))
                return path;
        }

        var registeredExe = FindRegisteredExe(platform, executableNames);
        if (registeredExe is not null)
            return registeredExe;

        lock (DiscoveryGate)
        {
            foreach (var name in executableNames)
            {
                if (DiscoveredExecutables.TryGetValue(name, out var path) && File.Exists(path))
                    return path;
            }
        }

        return null;
    }

    public static Task DiscoverAcrossDrivesAsync()
    {
        lock (DiscoveryGate)
            return _discoveryTask ??= Task.Run(ScanAcrossDrives);
    }

    public static string? GetDiscoveredExe(string executableName)
    {
        lock (DiscoveryGate)
            return DiscoveredExecutables.TryGetValue(executableName, out var path) && File.Exists(path)
                ? path
                : null;
    }

    public static bool IsInstalled(Models.PlatformDefinition platform) => FindExe(platform) is not null;

    private static string? FindRegisteredExe(
        Models.PlatformDefinition platform,
        IReadOnlyCollection<string> executableNames)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var uninstall = root.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                    if (uninstall is null)
                        continue;

                    foreach (var subKeyName in uninstall.GetSubKeyNames())
                    {
                        using var app = uninstall.OpenSubKey(subKeyName);
                        var displayName = app?.GetValue("DisplayName") as string;
                        if (!MatchesPlatformName(platform.Id, displayName))
                            continue;

                        var installLocation = app?.GetValue("InstallLocation") as string;
                        var displayIcon = app?.GetValue("DisplayIcon") as string;
                        var exe = FindExeInRegisteredLocation(installLocation, displayIcon, executableNames);
                        if (exe is not null)
                            return exe;
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or
                                           System.Security.SecurityException)
                {
                    Log.Write($"Could not inspect {hive} uninstall entries for {platform.Id}: {ex.Message}");
                }
            }
        }

        foreach (var name in executableNames)
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using var root = RegistryKey.OpenBaseKey(hive, view);
                        using var appPath = root.OpenSubKey(
                            $@"Software\Microsoft\Windows\CurrentVersion\App Paths\{name}");
                        var registeredPath = appPath?.GetValue(null) as string;
                        if (IsExpectedExecutable(registeredPath, executableNames))
                            return PathExpander.Expand(registeredPath!);
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or
                                               System.Security.SecurityException)
                    {
                        Log.Write($"Could not inspect registered app paths for {name}: {ex.Message}");
                    }
                }
            }
        }

        return null;
    }

    private static string? FindExeInRegisteredLocation(
        string? installLocation,
        string? displayIcon,
        IReadOnlyCollection<string> executableNames)
    {
        var iconPath = ExtractExecutablePath(displayIcon);
        if (IsExpectedExecutable(iconPath, executableNames))
            return PathExpander.Expand(iconPath!);

        if (string.IsNullOrWhiteSpace(installLocation))
            return null;

        var directory = PathExpander.Expand(installLocation.Trim().Trim('"'));
        if (!Directory.Exists(directory))
            return null;

        foreach (var name in executableNames)
        {
            var executable = Path.Combine(directory, name);
            if (File.Exists(executable))
                return executable;
        }

        return null;
    }

    private static string? ExtractExecutablePath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon))
            return null;

        var value = displayIcon.Trim();
        if (value.StartsWith('"'))
        {
            var closingQuote = value.IndexOf('"', 1);
            if (closingQuote > 1)
                value = value[1..closingQuote];
        }
        else
        {
            var iconIndex = value.LastIndexOf(',');
            if (iconIndex > 0)
                value = value[..iconIndex].Trim();
        }

        return PathExpander.Expand(value);
    }

    private static bool IsExpectedExecutable(string? path, IReadOnlyCollection<string> names) =>
        !string.IsNullOrWhiteSpace(path)
        && names.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
        && File.Exists(path);

    private static bool MatchesPlatformName(string platformId, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        return platformId switch
        {
            "steam" => displayName.Contains("Steam", StringComparison.OrdinalIgnoreCase),
            "epic" => displayName.Contains("Epic Games Launcher", StringComparison.OrdinalIgnoreCase),
            "battlenet" => displayName.Contains("Battle.net", StringComparison.OrdinalIgnoreCase),
            "riot" => displayName.Contains("Riot Client", StringComparison.OrdinalIgnoreCase),
            "ea" => displayName.Contains("EA app", StringComparison.OrdinalIgnoreCase)
                    || displayName.Contains("Origin", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static void ScanAcrossDrives()
    {
        var executableNames = PlatformCatalog.All
            .SelectMany(platform => platform.ExeCandidates)
            .Select(candidate => Path.GetFileName(PathExpander.Expand(candidate)))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<(string Path, int Depth)>();
        var scannedDirectories = 0;
        var skippedDirectories = 0;

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable)
                    pending.Push((drive.RootDirectory.FullName, 0));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skippedDirectories++;
                Log.Write($"Could not access drive {drive.Name} during launcher discovery: {ex.Message}");
            }
        }

        while (pending.Count > 0)
        {
            var (directory, depth) = pending.Pop();
            scannedDirectories++;

            foreach (var name in executableNames)
            {
                var candidate = Path.Combine(directory, name);
                if (!File.Exists(candidate))
                    continue;

                lock (DiscoveryGate)
                    DiscoveredExecutables.TryAdd(name, candidate);
            }

            if (depth >= 10)
                continue;

            try
            {
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    var childName = Path.GetFileName(child);
                    if (IsSystemDirectory(childName))
                        continue;

                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                            pending.Push((child, depth + 1));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                               System.Security.SecurityException)
                    {
                        skippedDirectories++;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                                       System.Security.SecurityException)
            {
                skippedDirectories++;
            }
        }

        Log.Write($"Launcher drive search inspected {scannedDirectories} folders and skipped {skippedDirectories} inaccessible folders; found {DiscoveredExecutables.Count} executables.");
    }

    private static bool IsSystemDirectory(string name) =>
        name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
        || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Windows", StringComparison.OrdinalIgnoreCase)
        || name.Equals("Recovery", StringComparison.OrdinalIgnoreCase);
}

public static class FileSwapper
{
    public static async Task CopyLiveToCacheAsync(IEnumerable<string> livePatterns, string cacheRoot)
    {
        Directory.CreateDirectory(cacheRoot);
        foreach (var pattern in livePatterns)
            await CopyPatternAsync(PathExpander.Expand(pattern), cacheRoot, toCache: true);
    }

    public static async Task RestoreCacheToLiveAsync(IEnumerable<string> livePatterns, string cacheRoot)
    {
        foreach (var pattern in livePatterns)
            await CopyPatternAsync(PathExpander.Expand(pattern), cacheRoot, toCache: false);
    }

    public static void ClearLive(IEnumerable<string> livePatterns)
    {
        foreach (var pattern in livePatterns)
            DeletePattern(PathExpander.Expand(pattern));
    }

    public static bool HasCachedFile(string liveFilePath, string cacheRoot) =>
        GetCachedFilePath(liveFilePath, cacheRoot) is not null;

    public static string? GetCachedFilePath(string liveFilePath, string cacheRoot)
    {
        var expandedPath = PathExpander.Expand(liveFilePath);
        var directCachePath = Path.Combine(cacheRoot, MakeRelativeName(expandedPath));
        if (File.Exists(directCachePath))
            return directCachePath;

        var liveDirectory = Path.GetDirectoryName(expandedPath);
        if (liveDirectory is null)
            return null;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        while (IsWithinRoot(liveDirectory, localAppData) ||
               IsWithinRoot(liveDirectory, roamingAppData))
        {
            var cachedDirectory = Path.Combine(cacheRoot, MakeRelativeName(liveDirectory));
            var nestedCachePath = Path.Combine(
                cachedDirectory,
                Path.GetRelativePath(liveDirectory, expandedPath));
            if (File.Exists(nestedCachePath))
                return nestedCachePath;

            if (PathEquals(liveDirectory, localAppData) || PathEquals(liveDirectory, roamingAppData))
                break;

            liveDirectory = Path.GetDirectoryName(liveDirectory);
            if (liveDirectory is null)
                break;
        }

        return null;
    }

    private static async Task CopyPatternAsync(string livePath, string cacheRoot, bool toCache)
    {
        var recursive = livePath.EndsWith($"{Path.DirectorySeparatorChar}*", StringComparison.Ordinal)
                        || livePath.EndsWith("/*", StringComparison.Ordinal);
        if (recursive)
            livePath = livePath[..^2];

        if (livePath.Contains('*'))
        {
            var dir = Path.GetDirectoryName(livePath)!;
            var mask = Path.GetFileName(livePath);
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.GetFiles(dir, mask))
            {
                var dest = toCache
                    ? Path.Combine(cacheRoot, Path.GetFileName(file))
                    : file;
                var src = toCache ? file : Path.Combine(cacheRoot, Path.GetFileName(file));
                if (toCache)
                    await CopyFileWithRetryAsync(src, dest);
                else if (File.Exists(src))
                    await CopyFileWithRetryAsync(src, dest);
            }

            var directoryCache = PatternDirectoryCache(cacheRoot, livePath);
            if (toCache)
            {
                foreach (var matchedDirectory in Directory.GetDirectories(dir, mask))
                {
                    var dest = Path.Combine(directoryCache, Path.GetFileName(matchedDirectory));
                    if (Directory.Exists(dest))
                        Directory.Delete(dest, true);
                    await CopyDirectoryAsync(matchedDirectory, dest);
                }
            }
            else if (Directory.Exists(directoryCache))
            {
                foreach (var cachedDirectory in Directory.GetDirectories(directoryCache))
                {
                    var dest = Path.Combine(dir, Path.GetFileName(cachedDirectory));
                    if (Directory.Exists(dest))
                        Directory.Delete(dest, true);
                    await CopyDirectoryAsync(cachedDirectory, dest);
                }
            }
            return;
        }

        var relativeName = MakeRelativeName(livePath);
        var cachePath = Path.Combine(cacheRoot, relativeName);

        if (Directory.Exists(livePath) || Directory.Exists(cachePath))
        {
            if (toCache && Directory.Exists(livePath))
                await CopyDirectoryAsync(livePath, cachePath);
            else if (!toCache && Directory.Exists(cachePath))
            {
                if (Directory.Exists(livePath))
                    Directory.Delete(livePath, true);
                await CopyDirectoryAsync(cachePath, livePath);
            }
            return;
        }

        if (toCache && File.Exists(livePath))
        {
            await CopyFileWithRetryAsync(livePath, cachePath);
        }
        else if (!toCache)
        {
            var cachedFile = File.Exists(cachePath)
                ? cachePath
                : GetCachedFilePath(livePath, cacheRoot);
            if (cachedFile is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(livePath)!);
                await CopyFileWithRetryAsync(cachedFile, livePath);
            }
        }
    }

    private static void DeletePattern(string livePath)
    {
        if (IsRecursivePattern(livePath))
        {
            var directory = livePath[..^2];
            if (!Directory.Exists(directory)) return;

            foreach (var file in Directory.GetFiles(directory))
                File.Delete(file);
            foreach (var childDirectory in Directory.GetDirectories(directory))
                Directory.Delete(childDirectory, true);
            return;
        }

        if (livePath.Contains('*') || livePath.Contains('?'))
        {
            var dir = Path.GetDirectoryName(livePath)!;
            var mask = Path.GetFileName(livePath);
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, mask))
                File.Delete(file);
            foreach (var matchedDirectory in Directory.GetDirectories(dir, mask))
                Directory.Delete(matchedDirectory, true);
            return;
        }

        if (File.Exists(livePath))
            File.Delete(livePath);
        else if (Directory.Exists(livePath))
            Directory.Delete(livePath, true);
    }

    private static bool IsRecursivePattern(string path) =>
        path.EndsWith($"{Path.DirectorySeparatorChar}*", StringComparison.Ordinal)
        || path.EndsWith($"{Path.AltDirectorySeparatorChar}*", StringComparison.Ordinal);

    private static string MakeRelativeName(string livePath)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var steam = SteamLocator.FindSteamRoot() ?? "Steam";
        var normalized = livePath;
        foreach (var root in new[] { local, roaming, steam })
        {
            if (normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return normalized[root.Length..].TrimStart('\\', '/').Replace('\\', '_').Replace('/', '_');
        }
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(livePath)))[..16];
    }

    private static bool IsWithinRoot(string path, string root) =>
        PathEquals(path, root) ||
        path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                        Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);

    private static bool PathEquals(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            StringComparison.OrdinalIgnoreCase);

    private static string PatternDirectoryCache(string cacheRoot, string livePattern)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(livePattern)))[..16];
        return Path.Combine(cacheRoot, "_patterns", hash);
    }

    private static async Task CopyDirectoryAsync(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(source, dir));
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(dest, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await CopyFileWithRetryAsync(file, target);
        }
    }

    private static async Task CopyFileWithRetryAsync(string source, string dest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        const int attempts = 6;
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                File.Copy(source, dest, overwrite: true);
                return;
            }
            catch (IOException) when (i < attempts - 1)
            {
                await Task.Delay(250);
            }
        }
    }
}

public static class EpicSessionValidator
{
    public static bool HasRememberMeToken(string sessionFilePath)
    {
        if (!File.Exists(sessionFilePath))
            return false;

        foreach (var line in File.ReadLines(sessionFilePath))
        {
            var dataStart = line.IndexOf("Data=", StringComparison.OrdinalIgnoreCase);
            if (dataStart >= 0 && line.Length - dataStart - "Data=".Length >= 1000)
                return true;
        }

        return false;
    }
}

public static class RegistrySwapper
{
    public static string? ReadValue(string spec)
    {
        if (!TryParse(spec, out var hive, out var keyPath, out var valueName))
            return null;

        using var key = hive.OpenSubKey(keyPath);
        return key?.GetValue(valueName)?.ToString();
    }

    public static void Save(IEnumerable<string> specs, string cacheRoot)
    {
        Directory.CreateDirectory(cacheRoot);
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            if (!TryParse(spec, out var hive, out var keyPath, out var valueName))
                continue;
            using var key = hive.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName);
            map[spec] = value?.ToString();
        }
        File.WriteAllText(Path.Combine(cacheRoot, "registry.json"),
            System.Text.Json.JsonSerializer.Serialize(map, JsonUtil.Options));
    }

    public static bool HasSavedValue(string cacheRoot, string spec)
    {
        var file = Path.Combine(cacheRoot, "registry.json");
        if (!File.Exists(file))
            return false;

        var map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(
            File.ReadAllText(file),
            JsonUtil.Options);
        return map is not null &&
               map.TryGetValue(spec, out var value) &&
               !string.IsNullOrWhiteSpace(value);
    }

    public static void Restore(string cacheRoot)
    {
        var file = Path.Combine(cacheRoot, "registry.json");
        if (!File.Exists(file)) return;
        var map = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(file), JsonUtil.Options)
                  ?? [];
        foreach (var (spec, value) in map)
        {
            if (!TryParse(spec, out var hive, out var keyPath, out var valueName))
                continue;
            using var key = hive.CreateSubKey(keyPath);
            if (value is null)
                key?.DeleteValue(valueName, throwOnMissingValue: false);
            else
                key?.SetValue(valueName, value);
        }
    }

    public static void SetValue(string spec, object value)
    {
        if (!TryParse(spec, out var hive, out var keyPath, out var valueName))
            return;
        using var key = hive.CreateSubKey(keyPath);
        key?.SetValue(valueName, value);
    }

    public static void Clear(IEnumerable<string> specs)
    {
        foreach (var spec in specs)
        {
            if (!TryParse(spec, out var hive, out var keyPath, out var valueName))
                continue;

            using var key = hive.OpenSubKey(keyPath, writable: true);
            key?.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static bool TryParse(string spec, out RegistryKey hive, out string keyPath, out string valueName)
    {
        hive = Registry.CurrentUser;
        keyPath = "";
        valueName = "";
        var parts = spec.Split(':');
        if (parts.Length != 2) return false;
        valueName = parts[1];
        var path = parts[0];
        if (path.StartsWith("HKCU\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.CurrentUser;
            keyPath = path[5..];
            return true;
        }
        if (path.StartsWith("HKLM\\", StringComparison.OrdinalIgnoreCase))
        {
            hive = Registry.LocalMachine;
            keyPath = path[5..];
            return true;
        }
        return false;
    }
}

public static class UniqueIdReader
{
    public static string? Read(Models.PlatformDefinition platform)
    {
        if (platform.UniqueId == UniqueIdKind.Steam)
            return SteamSwitcher.GetMostRecentSteamId();

        var source = platform.UniqueIdSource;
        if (string.IsNullOrWhiteSpace(source))
            return null;

        if (platform.UniqueId == UniqueIdKind.Registry)
            return RegistrySwapper.ReadValue(source);

        var path = PathExpander.Expand(source);
        if (platform.UniqueId == UniqueIdKind.Regex && !string.IsNullOrWhiteSpace(platform.UniqueIdRegex) && File.Exists(path))
        {
            var text = File.ReadAllText(path);
            var match = Regex.Match(text, platform.UniqueIdRegex, RegexOptions.IgnoreCase);
            if (match.Success)
                return match.Groups[^1].Value;
        }

        if (File.Exists(path))
            return HashFile(path);
        if (Directory.Exists(path))
            return HashTree(path);
        return null;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = System.Security.Cryptography.SHA256.HashData(stream);
        return Convert.ToHexString(hash)[..12];
    }

    private static string HashTree(string dir)
    {
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(f => f).Take(20).ToArray();
        using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        foreach (var file in files)
            sha.AppendData(File.ReadAllBytes(file));
        return Convert.ToHexString(sha.GetHashAndReset())[..12];
    }
}

public static class JsonUtil
{
    public static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };
}

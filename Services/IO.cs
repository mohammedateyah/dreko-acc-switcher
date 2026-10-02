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
    public static string? FindExe(Models.PlatformDefinition platform)
    {
        foreach (var candidate in platform.ExeCandidates)
        {
            var path = PathExpander.Expand(candidate);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static bool IsInstalled(Models.PlatformDefinition platform) => FindExe(platform) is not null;
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

        var cachedDirectory = Path.Combine(cacheRoot, MakeRelativeName(liveDirectory));
        var nestedCachePath = Path.Combine(cachedDirectory, Path.GetFileName(expandedPath));
        return File.Exists(nestedCachePath) ? nestedCachePath : null;
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

        if (toCache)
        {
            if (File.Exists(livePath))
                await CopyFileWithRetryAsync(livePath, cachePath);
        }
        else if (File.Exists(cachePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(livePath)!);
            await CopyFileWithRetryAsync(cachePath, livePath);
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

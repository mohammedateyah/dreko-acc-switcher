using System.Globalization;
using System.Text;
using DrekoAccSwitcher.Models;

namespace DrekoAccSwitcher.Services;

public static class Vdf
{
    public static Dictionary<string, object> Parse(string text)
    {
        var i = 0;
        SkipWs(text, ref i);
        return ParseObject(text, ref i);
    }

    public static string Stringify(Dictionary<string, object> root)
    {
        var sb = new StringBuilder();
        WriteObject(sb, root, 0, isRoot: true);
        return sb.ToString();
    }

    public static string StringifyRoot(string rootKey, Dictionary<string, object> value)
    {
        var sb = new StringBuilder();
        sb.Append('"').Append(Escape(rootKey)).Append('"');
        WriteObject(sb, value, 1, isRoot: false);
        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string? GetString(Dictionary<string, object> obj, string key)
    {
        if (!obj.TryGetValue(key, out var value)) return null;
        return value as string;
    }

    public static Dictionary<string, object>? GetObject(Dictionary<string, object> obj, string key)
    {
        if (!obj.TryGetValue(key, out var value)) return null;
        return value as Dictionary<string, object>;
    }

    private static Dictionary<string, object> ParseObject(string text, ref int i)
    {
        var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        SkipWs(text, ref i);
        if (i < text.Length && text[i] == '{')
            i++;

        while (i < text.Length)
        {
            SkipWs(text, ref i);
            if (i >= text.Length || text[i] == '}')
            {
                if (i < text.Length) i++;
                break;
            }

            var key = ReadQuoted(text, ref i);
            SkipWs(text, ref i);
            if (i < text.Length && text[i] == '{')
                map[key] = ParseObject(text, ref i);
            else
                map[key] = ReadQuoted(text, ref i);
        }

        return map;
    }

    private static string ReadQuoted(string text, ref int i)
    {
        SkipWs(text, ref i);
        if (i >= text.Length || text[i] != '"')
            throw new InvalidDataException("Expected quoted VDF token.");
        i++;
        var sb = new StringBuilder();
        while (i < text.Length)
        {
            var c = text[i++];
            if (c == '\\' && i < text.Length)
            {
                sb.Append(text[i++]);
                continue;
            }
            if (c == '"') break;
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static void SkipWs(string text, ref int i)
    {
        while (i < text.Length)
        {
            var c = text[i];
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                i++;
                continue;
            }
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n') i++;
                continue;
            }
            break;
        }
    }

    private static void WriteObject(StringBuilder sb, Dictionary<string, object> obj, int indent, bool isRoot)
    {
        if (!isRoot)
        {
            sb.AppendLine();
            sb.Append(new string('\t', indent - 1));
            sb.AppendLine("{");
        }

        foreach (var (key, value) in obj)
        {
            sb.Append(new string('\t', indent));
            sb.Append('"').Append(Escape(key)).Append('"');
            if (value is Dictionary<string, object> nested)
            {
                WriteObject(sb, nested, indent + 1, isRoot: false);
                sb.Append(new string('\t', indent));
                sb.AppendLine("}");
            }
            else
            {
                sb.Append("\t\t\"");
                sb.Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""));
                sb.AppendLine("\"");
            }
        }
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

public static class SteamSwitcher
{
    public static void PrepareForAddNewAccount()
    {
        RegistrySwapper.SetValue(@"HKCU\Software\Valve\Steam:AutoLoginUser", "");
    }

    public static IReadOnlyList<AccountView> ListRememberedAccounts()
    {
        var users = LoadUsers();
        if (users is null) return [];

        var list = new List<AccountView>();
        foreach (var (steamId, node) in users)
        {
            if (node is not Dictionary<string, object> account) continue;
            var userName = Vdf.GetString(account, "AccountName");
            var persona = Vdf.GetString(account, "PersonaName") ?? userName ?? steamId;
            list.Add(new AccountView
            {
                PlatformId = "steam",
                Id = steamId,
                DisplayName = persona,
                UserName = userName,
                FromLauncher = true,
                AvatarPath = FindAvatar(steamId)
            });
        }

        return list;
    }

    public static string? GetMostRecentSteamId()
    {
        var users = LoadUsers();
        if (users is null) return null;
        string? recent = null;
        foreach (var (steamId, node) in users)
        {
            if (node is not Dictionary<string, object> account) continue;
            if (Vdf.GetString(account, "MostRecent") == "1")
                return steamId;
            recent ??= steamId;
        }
        return recent;
    }

    public static void ApplyAccount(string steamId)
    {
        var path = LoginUsersPath();
        if (path is null || !File.Exists(path))
            throw new FileNotFoundException("Steam loginusers.vdf was not found. Open Steam once, sign in, and enable Remember password.");

        var root = Vdf.Parse(File.ReadAllText(path));
        var users = Vdf.GetObject(root, "users") ?? root;
        string? accountName = null;

        foreach (var (id, node) in users)
        {
            if (node is not Dictionary<string, object> account) continue;
            var isTarget = id.Equals(steamId, StringComparison.OrdinalIgnoreCase);
            account["MostRecent"] = isTarget ? "1" : "0";
            account["RememberPassword"] = "1";
            if (isTarget)
            {
                accountName = Vdf.GetString(account, "AccountName");
                account["Timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
            }
        }

        if (accountName is null)
            throw new InvalidOperationException("That Steam account is not in loginusers.vdf. Sign into it once with Remember password enabled.");

        File.WriteAllText(path, Vdf.StringifyRoot("users", users));

        RegistrySwapper.SetValue(@"HKCU\Software\Valve\Steam:AutoLoginUser", accountName);
        RegistrySwapper.SetValue(@"HKCU\Software\Valve\Steam:RememberPassword", 1);
    }

    public static string? LoginUsersPath()
    {
        var root = SteamLocator.FindSteamRoot();
        return root is null ? null : Path.Combine(root, "config", "loginusers.vdf");
    }

    private static Dictionary<string, object>? LoadUsers()
    {
        var path = LoginUsersPath();
        if (path is null || !File.Exists(path)) return null;
        var root = Vdf.Parse(File.ReadAllText(path));
        return Vdf.GetObject(root, "users") ?? root;
    }

    private static string? FindAvatar(string steamId64)
    {
        var steam = SteamLocator.FindSteamRoot();
        if (steam is null) return null;

        if (!ulong.TryParse(steamId64, out var id64))
            return null;
        var id32 = (id64 - 76561197960265728UL).ToString(CultureInfo.InvariantCulture);

        string[] candidates =
        [
            Path.Combine(steam, "config", "avatarcache", steamId64 + ".png"),
            Path.Combine(steam, "config", "avatarcache", id32 + ".png"),
            Path.Combine(steam, "userdata", id32, "config", "avatar.png")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }
}

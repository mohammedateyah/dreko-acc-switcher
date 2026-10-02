using DrekoAccSwitcher.Models;

namespace DrekoAccSwitcher.Services;

public sealed class SwitcherEngine
{
    private readonly AccountRepository _repo;

    public SwitcherEngine(AccountRepository repo)
    {
        _repo = repo;
    }

    public IReadOnlyList<AccountView> ListAccounts(PlatformDefinition platform)
    {
        if (platform.Id == "steam")
        {
            var remembered = SteamSwitcher.ListRememberedAccounts()
                .Where(a => !_repo.IsHidden(platform.Id, a.Id));
            return remembered.Select(a =>
            {
                var custom = _repo.CustomName(platform.Id, a.Id);
                return new AccountView
                {
                    PlatformId = a.PlatformId,
                    Id = a.Id,
                    DisplayName = custom ?? a.DisplayName,
                    UserName = a.UserName,
                    LastUsedAt = _repo.ForPlatform(platform.Id).FirstOrDefault(x => x.Id == a.Id)?.LastUsedAt,
                    AvatarPath = a.AvatarPath,
                    FromLauncher = true
                };
            }).ToList();
        }

        return _repo.ForPlatform(platform.Id).Select(a => new AccountView
        {
            PlatformId = a.PlatformId,
            Id = a.Id,
            DisplayName = a.DisplayName,
            UserName = a.UserName,
            LastUsedAt = a.LastUsedAt,
            FromLauncher = false
        }).ToList();
    }

    public async Task<SavedAccount> SaveCurrentAsync(PlatformDefinition platform, string displayName)
    {
        var isEpic = platform.Id.Equals("epic", StringComparison.OrdinalIgnoreCase);
        var epicSessionFile = PathExpander.Expand(
            "{LocalAppData}\\EpicGamesLauncher\\Saved\\Config\\WindowsEditor\\GameUserSettings.ini");
        var id = isEpic ? UniqueIdReader.Read(platform) : null;
        if (isEpic &&
            (string.IsNullOrWhiteSpace(id) ||
             !EpicSessionValidator.HasRememberMeToken(epicSessionFile)))
            throw new InvalidOperationException(Localization.Text("epicAccountNotSignedIn"));

        await CloseAsync(platform);
        await Task.Delay(300);

        id ??= UniqueIdReader.Read(platform);
        if (isEpic && string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException(Localization.Text("epicAccountNotSignedIn"));
        if (string.IsNullOrWhiteSpace(id))
            id = "acc-" + DateTime.Now.ToString("yyyyMMddHHmmss");

        return await SaveSnapshotAsync(platform, id, displayName);
    }

    private async Task<SavedAccount> SaveSnapshotAsync(
        PlatformDefinition platform,
        string id,
        string displayName)
    {
        var cache = AppPaths.AccountFolder(platform.Id, id);
        var staging = cache + ".saving-" + Guid.NewGuid().ToString("N");
        var previous = cache + ".previous-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(staging);
            await FileSwapper.CopyLiveToCacheAsync(platform.LoginPaths, staging);
            if (platform.RegistryValues.Length > 0)
                RegistrySwapper.Save(platform.RegistryValues, staging);

            if (Directory.Exists(cache))
                Directory.Move(cache, previous);
            Directory.Move(staging, cache);

            var saved = _repo.Upsert(new SavedAccount
            {
                PlatformId = platform.Id,
                Id = id,
                DisplayName = displayName.Trim(),
                UserName = platform.Id.Equals("epic", StringComparison.OrdinalIgnoreCase) ? null : id,
                LastUsedAt = DateTimeOffset.Now
            });

            Log.Write($"Saved {platform.Id}/{id} as '{displayName}'.");
            return saved;
        }
        catch
        {
            if (Directory.Exists(previous) && !Directory.Exists(cache))
                Directory.Move(previous, cache);
            throw;
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            if (Directory.Exists(previous) && Directory.Exists(cache))
                Directory.Delete(previous, true);
        }
    }

    public async Task SwitchToAsync(PlatformDefinition platform, AccountView account)
    {
        var exe = LauncherLocator.FindExe(platform)
                  ?? throw new InvalidOperationException($"{platform.DisplayName} is not installed, or the exe was not found.");

        var cache = AppPaths.AccountFolder(platform.Id, account.Id);
        if (platform.Id != "steam")
        {
            if (!Directory.Exists(cache))
                throw new DirectoryNotFoundException("No saved files for that account. Save it once while you are logged in.");

            if (platform.Id.Equals("epic", StringComparison.OrdinalIgnoreCase))
            {
                var cachedSessionFile = FileSwapper.GetCachedFilePath(
                    "{LocalAppData}\\EpicGamesLauncher\\Saved\\Config\\WindowsEditor\\GameUserSettings.ini",
                    cache);
                if (cachedSessionFile is null ||
                    !EpicSessionValidator.HasRememberMeToken(cachedSessionFile) ||
                    !RegistrySwapper.HasSavedValue(
                        cache,
                        PlatformCatalog.EpicAccountIdRegistryValue))
                    throw new InvalidOperationException(Localization.Text("epicAccountNeedsResave"));
            }
        }

        await CloseAsync(platform);

        if (platform.Id == "steam")
        {
            SteamSwitcher.ApplyAccount(account.Id);
        }
        else
        {
            FileSwapper.ClearLive(platform.LoginPaths.Concat(platform.ExtraClearPaths));
            await FileSwapper.RestoreCacheToLiveAsync(platform.LoginPaths, cache);
            RegistrySwapper.Restore(cache);
        }

        _repo.MarkUsed(platform.Id, account.Id);
        Log.Write($"Switched {platform.Id} -> {account.Id} ({account.DisplayName}).");
        ProcessHelper.Launch(exe);
    }

    public async Task AddNewAsync(PlatformDefinition platform)
    {
        var exe = LauncherLocator.FindExe(platform)
                  ?? throw new InvalidOperationException($"{platform.DisplayName} is not installed.");

        if (platform.Id.Equals("epic", StringComparison.OrdinalIgnoreCase))
        {
            var sessionFile = PathExpander.Expand(
                "{LocalAppData}\\EpicGamesLauncher\\Saved\\Config\\WindowsEditor\\GameUserSettings.ini");
            var activeAccountId = UniqueIdReader.Read(platform);
            var hasActiveSession = !string.IsNullOrWhiteSpace(activeAccountId) &&
                                   EpicSessionValidator.HasRememberMeToken(sessionFile);

            await CloseAsync(platform);
            await Task.Delay(300);

            if (hasActiveSession)
            {
                var displayName = _repo.ForPlatform(platform.Id)
                    .FirstOrDefault(account => account.Id == activeAccountId)?.DisplayName ?? activeAccountId!;
                await SaveSnapshotAsync(platform, activeAccountId!, displayName);
            }

            FileSwapper.ClearLive(platform.LoginPaths);
            RegistrySwapper.Clear(platform.RegistryValues);
            Log.Write("Cleared Epic's active login session after preserving the current signed-in account.");
            ProcessHelper.Launch(exe);
            return;
        }

        await CloseAsync(platform);
        if (platform.Id.Equals("steam", StringComparison.OrdinalIgnoreCase))
        {
            SteamSwitcher.PrepareForAddNewAccount();
            Log.Write("Cleared Steam auto-login selection for add-new; remembered accounts were preserved.");
        }
        else
        {
            FileSwapper.ClearLive(platform.LoginPaths.Concat(platform.ExtraClearPaths));
            Log.Write($"Cleared {platform.Id} session for add-new.");
        }
        ProcessHelper.Launch(exe);
    }

    public void Forget(PlatformDefinition platform, AccountView account) =>
        _repo.Forget(platform.Id, account.Id);

    public void Rename(PlatformDefinition platform, AccountView account, string name) =>
        _repo.Rename(platform.Id, account.Id, name);

    public void Launch(PlatformDefinition platform)
    {
        var exe = LauncherLocator.FindExe(platform)
                  ?? throw new InvalidOperationException($"{platform.DisplayName} is not installed.");
        ProcessHelper.Launch(exe);
    }

    private static Task CloseAsync(PlatformDefinition platform)
    {
        var exe = LauncherLocator.FindExe(platform);
        return ProcessHelper.CloseAsync(
            platform.ProcessNames,
            exe,
            platform.ShutdownArguments);
    }
}

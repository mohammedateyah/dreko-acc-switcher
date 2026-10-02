using System.Text.Json;
using DrekoAccSwitcher.Models;

namespace DrekoAccSwitcher.Services;

public sealed class AccountRepository
{
    private AccountCatalog _catalog = new();

    public void Load()
    {
        AppPaths.EnsureLayout();
        if (!File.Exists(AppPaths.CatalogFile))
        {
            _catalog = new AccountCatalog();
            return;
        }

        try
        {
            _catalog = JsonSerializer.Deserialize<AccountCatalog>(File.ReadAllText(AppPaths.CatalogFile), JsonUtil.Options)
                       ?? throw new InvalidDataException("The account catalog contains no data.");
        }
        catch (Exception ex)
        {
            Log.Write($"Catalog load failed: {ex.Message}");
            throw new InvalidDataException(
                $"Could not read the account catalog at '{AppPaths.CatalogFile}'. The file was left unchanged.",
                ex);
        }
    }

    public void Save()
    {
        AppPaths.EnsureLayout();
        var temporaryFile = AppPaths.CatalogFile + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(_catalog, JsonUtil.Options));
        File.Move(temporaryFile, AppPaths.CatalogFile, overwrite: true);
    }

    public IReadOnlyList<SavedAccount> ForPlatform(string platformId) =>
        _catalog.Accounts.Where(a => a.PlatformId.Equals(platformId, StringComparison.OrdinalIgnoreCase) && !a.Hidden).ToList();

    public SavedAccount Upsert(SavedAccount account)
    {
        var existing = _catalog.Accounts.FirstOrDefault(a =>
            a.PlatformId == account.PlatformId && a.Id == account.Id);
        if (existing is null)
        {
            _catalog.Accounts.Add(account);
            Save();
            return account;
        }

        existing.DisplayName = account.DisplayName;
        existing.UserName = account.UserName ?? existing.UserName;
        existing.Hidden = false;
        if (account.LastUsedAt is not null)
            existing.LastUsedAt = account.LastUsedAt;
        Save();
        return existing;
    }

    public void MarkUsed(string platformId, string id)
    {
        var existing = _catalog.Accounts.FirstOrDefault(a => a.PlatformId == platformId && a.Id == id);
        if (existing is null) return;
        existing.LastUsedAt = DateTimeOffset.Now;
        Save();
    }

    public void Rename(string platformId, string id, string displayName)
    {
        var existing = _catalog.Accounts.FirstOrDefault(a => a.PlatformId == platformId && a.Id == id);
        if (existing is null)
        {
            _catalog.Accounts.Add(new SavedAccount
            {
                PlatformId = platformId,
                Id = id,
                DisplayName = displayName
            });
        }
        else
        {
            existing.DisplayName = displayName;
        }
        Save();
    }

    public void Forget(string platformId, string id)
    {
        var existing = _catalog.Accounts.FirstOrDefault(a => a.PlatformId == platformId && a.Id == id);
        if (existing is not null)
            existing.Hidden = true;
        var folder = AppPaths.AccountFolder(platformId, id);
        if (Directory.Exists(folder))
        {
            try { Directory.Delete(folder, true); }
            catch (Exception ex) { Log.Write($"Cache delete failed: {ex.Message}"); }
        }
        Save();
    }

    public string? CustomName(string platformId, string id) =>
        _catalog.Accounts.FirstOrDefault(a => a.PlatformId == platformId && a.Id == id && !a.Hidden)?.DisplayName;

    public bool IsHidden(string platformId, string id) =>
        _catalog.Accounts.Any(a => a.PlatformId == platformId && a.Id == id && a.Hidden);
}

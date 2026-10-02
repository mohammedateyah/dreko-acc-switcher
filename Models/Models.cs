namespace DrekoAccSwitcher.Models;

public sealed class PlatformDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string AccentHex { get; init; }
    public required string Glyph { get; init; }
    public required string[] ProcessNames { get; init; }
    public required string[] ExeCandidates { get; init; }
    public string? ShutdownArguments { get; init; }
    public required string[] LoginPaths { get; init; }
    public string[] ExtraClearPaths { get; init; } = [];
    public string[] RegistryValues { get; init; } = [];
    public UniqueIdKind UniqueId { get; init; } = UniqueIdKind.FileHash;
    public string? UniqueIdSource { get; init; }
    public string? UniqueIdRegex { get; init; }
}

public enum UniqueIdKind
{
    FileHash,
    Regex,
    Steam,
    Registry
}

public sealed class SavedAccount
{
    public required string PlatformId { get; set; }
    public required string Id { get; set; }
    public required string DisplayName { get; set; }
    public string? UserName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastUsedAt { get; set; }
    public bool Hidden { get; set; }
}

public sealed class AccountCatalog
{
    public List<SavedAccount> Accounts { get; set; } = [];
}

public sealed class AccountView
{
    public required string PlatformId { get; init; }
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string? UserName { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    public string? AvatarPath { get; init; }
    public bool FromLauncher { get; init; }
}

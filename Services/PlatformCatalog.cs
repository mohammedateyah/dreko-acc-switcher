using DrekoAccSwitcher.Models;

namespace DrekoAccSwitcher.Services;

public static class PlatformCatalog
{
    public const string EpicAccountIdRegistryValue =
        @"HKCU\Software\Epic Games\Unreal Engine\Identifiers:AccountId";

    public static IReadOnlyList<PlatformDefinition> All { get; } =
    [
        new PlatformDefinition
        {
            Id = "steam",
            DisplayName = "Steam",
            AccentHex = "#66C0F4",
            Glyph = "S",
            ProcessNames = ["steam", "steamwebhelper", "steamservice"],
            ShutdownArguments = "-shutdown",
            ExeCandidates =
            [
                "{Steam}\\steam.exe",
                "{ProgramFilesX86}\\Steam\\steam.exe",
                "{ProgramFiles}\\Steam\\steam.exe"
            ],
            LoginPaths =
            [
                "{Steam}\\config\\loginusers.vdf",
                "{Steam}\\config\\config.vdf",
                "{Steam}\\ssfn*"
            ],
            RegistryValues =
            [
                @"HKCU\Software\Valve\Steam:AutoLoginUser",
                @"HKCU\Software\Valve\Steam:RememberPassword"
            ],
            UniqueId = UniqueIdKind.Steam
        },
        new PlatformDefinition
        {
            Id = "epic",
            DisplayName = "Epic Games",
            AccentHex = "#E2E2E2",
            Glyph = "E",
            ProcessNames = ["EpicGamesLauncher", "EpicWebHelper"],
            ExeCandidates =
            [
                "{ProgramFilesX86}\\Epic Games\\Launcher\\Portal\\Binaries\\Win32\\EpicGamesLauncher.exe",
                "{ProgramFilesX86}\\Epic Games\\Launcher\\Portal\\Binaries\\Win64\\EpicGamesLauncher.exe",
                "{ProgramFiles}\\Epic Games\\Launcher\\Portal\\Binaries\\Win64\\EpicGamesLauncher.exe"
            ],
            LoginPaths =
            [
                "{LocalAppData}\\EpicGamesLauncher\\Saved\\Config",
                "{LocalAppData}\\EpicGamesLauncher\\Saved\\webcache*"
            ],
            RegistryValues =
            [
                EpicAccountIdRegistryValue
            ],
            UniqueId = UniqueIdKind.Registry,
            UniqueIdSource = EpicAccountIdRegistryValue
        },
        new PlatformDefinition
        {
            Id = "battlenet",
            DisplayName = "Battle.net",
            AccentHex = "#00AEFF",
            Glyph = "B",
            ProcessNames = ["Battle.net", "Agent", "Battle.net Helper"],
            ExeCandidates =
            [
                "{ProgramFilesX86}\\Battle.net\\Battle.net.exe",
                "{ProgramFiles}\\Battle.net\\Battle.net.exe"
            ],
            LoginPaths =
            [
                "{AppData}\\Battle.net\\Battle.net.config",
                "{LocalAppData}\\Battle.net"
            ],
            UniqueId = UniqueIdKind.Regex,
            UniqueIdSource = "{AppData}\\Battle.net\\Battle.net.config",
            UniqueIdRegex = "\"(AccountName|Email|BattleTag)\"\\s*:\\s*\"([^\"]+)\""
        },
        new PlatformDefinition
        {
            Id = "riot",
            DisplayName = "Riot Client",
            AccentHex = "#D13639",
            Glyph = "R",
            ProcessNames = ["RiotClientServices", "RiotClientCrashHandler", "LeagueClient", "LeagueClientUx"],
            ExeCandidates =
            [
                "{ProgramFiles}\\Riot Games\\Riot Client\\RiotClientServices.exe",
                "{ProgramFilesX86}\\Riot Games\\Riot Client\\RiotClientServices.exe",
                "%SystemDrive%\\Riot Games\\Riot Client\\RiotClientServices.exe"
            ],
            LoginPaths =
            [
                "{LocalAppData}\\Riot Games\\Riot Client\\Data\\RiotGamesPrivateSettings.yaml",
                "{LocalAppData}\\Riot Games\\Riot Client\\Config\\lockfile"
            ],
            ExtraClearPaths =
            [
                "{LocalAppData}\\Riot Games\\Riot Client\\Data"
            ],
            UniqueId = UniqueIdKind.FileHash,
            UniqueIdSource = "{LocalAppData}\\Riot Games\\Riot Client\\Data\\RiotGamesPrivateSettings.yaml"
        },
        new PlatformDefinition
        {
            Id = "ea",
            DisplayName = "EA App",
            AccentHex = "#FF4747",
            Glyph = "EA",
            ProcessNames = ["EADesktop", "EALauncher", "Origin", "OriginWebHelperService"],
            ExeCandidates =
            [
                "{ProgramFiles}\\Electronic Arts\\EA Desktop\\EA Desktop\\EADesktop.exe",
                "{ProgramFilesX86}\\Electronic Arts\\EA Desktop\\EA Desktop\\EADesktop.exe",
                "{ProgramFilesX86}\\Origin\\Origin.exe"
            ],
            LoginPaths =
            [
                "{LocalAppData}\\Electronic Arts\\EA Desktop",
                "{AppData}\\Origin"
            ],
            UniqueId = UniqueIdKind.FileHash,
            UniqueIdSource = "{LocalAppData}\\Electronic Arts\\EA Desktop"
        },
    ];

    public static PlatformDefinition? Get(string id) =>
        All.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
}

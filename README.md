# Dreko Acc Switcher

Windows account switcher for Steam, Epic Games, Battle.net, Riot Client, and EA App.

It does **not** store passwords. It copies the launcher’s own remembered-login files on this PC, then restores them when you switch.

## Install

Download `DrekoAccSwitcher-Setup-<version>.exe` from the [latest GitHub Release](https://github.com/mohammedateyah/dreko-acc-switcher/releases/latest) and run it. The per-user installer offers desktop shortcut and taskbar pin options, enabled by default, and lets you choose whether to launch Dreko when setup finishes. Application data and saved accounts in `%AppData%\Dreko Acc Switcher\` are preserved when upgrading or uninstalling.

On first launch, Dreko shows a welcome message with the free-use and DrekoStudio copyright notice and a link to the studio website. The message is shown once per user profile.

## How to use

The app opens on its home page, which shows the installed version, the total number of visible accounts across supported launchers, and a short usage guide. It checks the latest public GitHub release and reports whether an update is available. When a newer version is available, use **Download and install** to download and verify the installer before confirming the update. Dreko closes while the update installs and restarts when setup finishes; saved account data is preserved.

1. Open the launcher and sign in, keeping the session active.
2. In Dreko, pick the launcher and click **Save current account**.
3. Repeat for every account.
4. Click **Switch** on a card. Dreko closes the launcher, restores that session, and starts it again.

**Add new** opens the launcher's sign-in screen so you can sign into another account, then save it. For Steam, it clears only the auto-login selection and preserves Steam's remembered account list and authentication files. For Epic, Dreko saves the active session first, clears only Epic's sign-in session file and active account identifier, and opens the sign-in screen; saved Epic accounts remain available to switch back to.

Steam is special: accounts already remembered in `loginusers.vdf` show up automatically. You still need Steam’s own “Remember me” enabled once per account. Epic account saves include its launcher sign-in settings and account identifier from the current Windows user profile. After updating from an older Dreko version, sign into each Epic account and save it again before switching. An expired Epic session may still require signing in again.

## Build

Requires the .NET 8 SDK. The app runs on Windows.

```bat
dotnet build DrekoAccSwitcher.sln -c Release
dotnet run --project DrekoAccSwitcher.csproj -c Release
```

The exe is written to `bin\Release\net8.0-windows\`.

Saved data lives in `%AppData%\Dreko Acc Switcher\`.

Open **Settings** to choose English or Arabic, launch Dreko when Windows starts, or keep it in the notification area when the window is closed.

## Releases

Push a version tag such as `v1.0.1` to build a self-contained Windows x64 package and a branded installer, then publish both as a GitHub Release. The app compares that release tag with its installed version on startup. Keep the repository public so update checks work without requiring users to sign in. The release workflow uses Inno Setup 6.6.1, which is licensed for non-commercial use; use a commercially licensed installer tool before distributing the installer commercially.

## Notes

- Close games if a file copy fails (session files can be locked).
- This only works with **your** accounts on **this** machine.
- 2FA / email codes are still required the first time an account is remembered by the official launcher.

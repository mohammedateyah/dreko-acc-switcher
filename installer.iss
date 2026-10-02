#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

[Setup]
AppId={{A26825E3-514E-4D16-BE05-53F86B6410CD}
AppName=Dreko Acc Switcher
AppVersion={#AppVersion}
AppPublisher=DrekoStudio
AppPublisherURL=https://dreko8u.web.app
AppSupportURL=https://github.com/mohammedateyah/dreko-acc-switcher
AppUpdatesURL=https://github.com/mohammedateyah/dreko-acc-switcher/releases
DefaultDirName={localappdata}\Programs\Dreko Acc Switcher
DefaultGroupName=Dreko Acc Switcher
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=Assets\drekoaccswitcher.ico
WizardStyle=modern dark
WizardImageFile=installer\WizardImage.png
WizardImageBackColor=#0B0F14
WizardSmallImageFile=Assets\drekoaccswitcher.png
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=output
OutputBaseFilename=DrekoAccSwitcher-Setup-{#AppVersion}
UninstallDisplayIcon={app}\DrekoAccSwitcher.exe
UninstallDisplayName=Dreko Acc Switcher
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
VersionInfoCompany=DrekoStudio
VersionInfoDescription=Dreko Acc Switcher Installer
VersionInfoProductName=Dreko Acc Switcher
VersionInfoProductVersion={#AppVersion}
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[CustomMessages]
english.CreateDesktopShortcut=Create a desktop shortcut
arabic.CreateDesktopShortcut=إنشاء اختصار على سطح المكتب
english.PinToTaskbar=Pin Dreko to the taskbar
arabic.PinToTaskbar=تثبيت Dreko على شريط المهام
english.AdditionalOptions=Additional options
arabic.AdditionalOptions=خيارات إضافية
english.TaskbarPinFailed=Windows did not expose a taskbar pin command. Use the desktop shortcut to pin Dreko manually if your version of Windows allows it.
arabic.TaskbarPinFailed=لم يوفّر Windows أمراً للتثبيت التلقائي على شريط المهام. استخدم اختصار سطح المكتب لتثبيت Dreko يدوياً إذا كان إصدار Windows لديك يسمح بذلك.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopShortcut}"; GroupDescription: "{cm:AdditionalOptions}:"
Name: "taskbarpin"; Description: "{cm:PinToTaskbar}"; GroupDescription: "{cm:AdditionalOptions}:"

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installer\PinToTaskbar.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Dreko Acc Switcher"; Filename: "{app}\DrekoAccSwitcher.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Dreko Acc Switcher"; Filename: "{app}\DrekoAccSwitcher.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\DrekoAccSwitcher.exe"; Description: "{cm:LaunchProgram,Dreko Acc Switcher}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{app}\PinToTaskbar.ps1"" ""{app}\DrekoAccSwitcher.exe"" -Unpin"; Flags: runhidden waituntilterminated; RunOnceId: "DrekoAccSwitcherUnpin"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('taskbarpin') then
  begin
    if not Exec(
      ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
        ExpandConstant('{app}\PinToTaskbar.ps1') + '" "' +
        ExpandConstant('{app}\DrekoAccSwitcher.exe') + '"',
      '',
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode) or (ResultCode <> 0) then
      if not WizardSilent then
        MsgBox(ExpandConstant('{cm:TaskbarPinFailed}'), mbInformation, MB_OK);
  end;

  if (CurStep = ssDone) and WizardSilent and
    (ExpandConstant('{param:AUTORESTART|0}') = '1') then
    if not Exec(
        ExpandConstant('{app}\DrekoAccSwitcher.exe'),
        '',
        ExpandConstant('{app}'),
        SW_SHOWNORMAL,
        ewNoWait,
        ResultCode) then
      Log('Could not restart Dreko Acc Switcher after installing the update.');
end;

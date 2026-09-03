; ============================================================================
; ODZEN — Windows Installer Script
; Developed by Taroxzen (https://github.com/taroxzen)
; Powered by Inno Setup (100% Open-Source)
; ============================================================================

#define MyAppName "ODZEN"
#define MyAppVersion "1.4.2"
#define MyAppPublisher "Taroxzen"
#define MyAppURL "https://github.com/taroxzen"
#define MyAppExeName "ODZEN.exe"
#define MySourceDir "ODZEN_FINAL"
#define MyOutputDir "ODZEN_INSTALLER_OUTPUT"

[Setup]
; App Identity & Details
AppId={{D8C8B190-7D14-4A3C-9189-9F83816B3E99}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={userappdata}\Programs\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=LICENSE
OutputDir={#MyOutputDir}
OutputBaseFilename=ODZEN_Setup_v1.4.2
SetupIconFile=Odzen.Avalonia\Assets\odzen_logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
VersionInfoVersion=1.4.2.0
VersionInfoCompany=Taroxzen
VersionInfoDescription=ODZEN Setup
VersionInfoCopyright=Copyright © 2026 Taroxzen (https://github.com/taroxzen)
VersionInfoProductName=ODZEN
VersionInfoProductVersion=1.4.2.0

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "bulgarian"; MessagesFile: "compiler:Languages\Bulgarian.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

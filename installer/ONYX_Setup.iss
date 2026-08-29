; ============================================================================
; ONYX Launcher — Inno Setup Script
; Developed by Taroxzen (https://github.com/taroxzen)
; Copyright (c) 2026 Taroxzen. All rights reserved.
; ============================================================================

#define MyAppName "ONYX Launcher"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "Taroxzen"
#define MyAppURL "https://github.com/taroxzen"
#define MyAppExeName "Onyx.Avalonia.exe"

[Setup]
; Temel Uygulama ve Geliştirici Bilgileri
AppId={{6B4B1C5A-9872-4DF4-9AE8-9F1B478C89A0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/ONYX-Launcher
AppUpdatesURL={#MyAppURL}/ONYX-Launcher/releases

; Kurulum Klasörü (Seçenek A: AppData\Local\Programs - Yönetici İzni Gerekmez)
DefaultDirName={userappdata}\Programs\ONYX Launcher
DefaultGroupName=ONYX Launcher
DisableProgramGroupPage=yes
PrivilegesRequired=lowest

; Görsel ve Tasarım Ayarları
WizardStyle=modern
SetupIconFile=..\Onyx.Avalonia\Assets\onyx_logo.ico
WizardImageFile=wizard_banner.bmp
WizardSmallImageFile=wizard_small.bmp
LicenseFile=..\LICENSE

; Çıktı Dosyası Yapılandırması
OutputDir=..\..\
OutputBaseFilename=ONYX_Setup_v1.2.0
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
VersionInfoVersion=1.2.0.0
VersionInfoCompany=Taroxzen
VersionInfoDescription=ONYX Launcher Installer
VersionInfoCopyright=Copyright (C) 2026 Taroxzen

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
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Windows ile birlikte otomatik başlat"; GroupDescription: "Ek Seçenekler:"; Flags: unchecked

[CustomMessages]
turkish.LaunchOnFinish=ONYX Launcher'ı Şimdi Başlat
english.LaunchOnFinish=Launch ONYX Launcher Now
german.LaunchOnFinish=ONYX Launcher jetzt starten
bulgarian.LaunchOnFinish=Стартирай ONYX Launcher сега
spanish.LaunchOnFinish=Ejecutar ONYX Launcher ahora
dutch.LaunchOnFinish=Start ONYX Launcher nu
french.LaunchOnFinish=Lancer ONYX Launcher maintenant
russian.LaunchOnFinish=Запустить ONYX Launcher сейчас

[Files]
Source: "..\..\ONYX-Release-v1.2.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchOnFinish}"; Flags: nowait postinstall skipifsilent

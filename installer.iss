; Instalator Windows pentru Master Control Studio Pro, cu Inno Setup
; (https://jrsoftware.org/isinfo.php — gratuit).
;
; Compilare MANUALA, pe Windows, cu Inno Setup Compiler instalat:
;   1. dotnet publish src\MacMasterControlPro.Client -c Release -r win-x64 --self-contained -o publish
;   2. Deschide acest fisier (installer.iss) cu Inno Setup Compiler
;   3. Apasa "Compile" (sau F9)
;   4. Rezultatul apare in Output\MacMasterControlProSetup.exe

#define MyAppName "Master Control Studio Pro"
#define MyAppVersion "1.9.1"
#define MyAppPublisher "Cristi Gordas"
#define MyAppExeName "MacMasterControlPro.exe"
#define MyAppURL "https://gordas.dev/mac-master-control-pro"

[Setup]
AppId={{B7E1C4A2-3D9F-4A6B-8E2C-MMCPROWIN0001}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\GDC\Master Control Studio Pro
DefaultGroupName=Master Control Studio Pro
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=MacMasterControlProSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=src\MacMasterControlPro.Client\Assets\app.ico
; Regula 19 — pas de acceptare a licentei obligatoriu (radio "I accept"/
; "I do not accept", Next dezactivat pana la acceptare explicita).
LicenseFile=installer\license.txt
; Nu semnat cu certificat platit — Windows SmartScreen arata un
; avertisment "Unrecognized app" la prima rulare, normal pentru
; distributie indie (aceeasi nota ca restul ecosistemului GDC).
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Dezinstaleaza {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

; Regula de Clean Uninstall (gdc-plugin-manager-catalog-vendor/CLAUDE.md):
; sterge TOT ce a scris aplicatia, nu doar folderul din Program Files.
[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Master Control Studio Pro"
Type: filesandordirs; Name: "{userappdata}\MacMasterControlPro"

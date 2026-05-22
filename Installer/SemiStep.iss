; SemiStep Inno Setup installer script
; Build with: iscc.exe /DAppVersion=1.0.0 SemiStep.iss
; The AppVersion define is mandatory — pass it via the /D switch.

#ifndef AppVersion
  #error AppVersion must be defined. Pass it as: iscc.exe /DAppVersion=1.2.3 SemiStep.iss
#endif

#define AppName      "SemiStep"
#define AppPublisher "Inc Semiteq"
#define AppExeName   "Semistep.exe"
#define AppId        "{{8B3F2C1A-4D7E-4F9B-A2C6-1E5D8F3B7A4C}"

; Paths relative to the location of this .iss file (Installer/)
#define SrcBinDir    "..\SemiStep\Artifacts\publish\SemiStep.UI\release_win-x64"

#define SrcCfgDir    "..\ConfigFiles"
#define AppIconFile       "..\SemiStep\SemiStep.UI\logo.ico"
#define LicenseFile       ".\LICENSE.txt"
#define WizardImageLarge  ".\WizardImageFile.bmp"
#define WizardImageSmall  ".\WizardSmallImageFile.bmp"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppVerName={#AppName} {#AppVersion}

; Installation directory for application binaries
DefaultDirName={autopf}\{#AppName}
DisableDirPage=no

; Start menu group
DefaultGroupName={#AppName}
DisableProgramGroupPage=no

; Output
OutputDir=Output
OutputBaseFilename=SemiStep-Setup

; Compression
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Appearance
WizardStyle=modern
SetupIconFile={#AppIconFile}
LicenseFile={#LicenseFile}
WizardImageFile={#WizardImageLarge}
WizardSmallImageFile={#WizardImageSmall}
UninstallDisplayIcon={app}\{#AppExeName}

; Require admin rights because we write to Program Files and C:\DISTR
PrivilegesRequired=admin

; In-place upgrade: automatically uninstall previous version before installing
CloseApplications=yes
CloseApplicationsFilter=*{#AppExeName}*

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "preset_mbe";   Description: "MBE";   GroupDescription: "Configuration preset:"; Flags: exclusive
Name: "preset_mocvd"; Description: "MOCVD"; GroupDescription: "Configuration preset:"; Flags: exclusive
Name: "preset_rie";   Description: "RIE";   GroupDescription: "Configuration preset:"; Flags: exclusive
Name: "desktopicon";  Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Remove legacy flat-layout config subfolders from prior installations.
; The new preset-nested layout (MBE\, MOCVD\) is left intact.
Type: filesandordirs; Name: "C:\DISTR\Config\Semistep\actions"
Type: filesandordirs; Name: "C:\DISTR\Config\Semistep\columns"
Type: filesandordirs; Name: "C:\DISTR\Config\Semistep\connection"
Type: filesandordirs; Name: "C:\DISTR\Config\Semistep\groups"
Type: filesandordirs; Name: "C:\DISTR\Config\Semistep\properties"
Type: filesandordirs; Name: "C:\DISTR\Config\Semistep\ui"

[Files]
; Application binaries
Source: "{#SrcBinDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Configuration files — both presets are always installed under a hardcoded absolute path
; the application reads (see StartupOptions.DefaultConfigDir). The selected [Tasks] entry
; controls only which preset the created shortcuts target.
Source: "..\ConfigFiles\MBE\*";   DestDir: "C:\DISTR\Config\Semistep\MBE";   Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ConfigFiles\MOCVD\*"; DestDir: "C:\DISTR\Config\Semistep\MOCVD"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\ConfigFiles\RIE\*";   DestDir: "C:\DISTR\Config\Semistep\RIE";   Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Ensure the logs directory exists before the app first runs
;   C:\DISTR\Logs  (see StartupOptions.DefaultLogFilePath)
Name: "C:\DISTR\Logs"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MBE"""; Tasks: preset_mbe
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MOCVD"""; Tasks: preset_mocvd
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\RIE"""; Tasks: preset_rie
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MBE"""; Tasks: desktopicon and preset_mbe
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MOCVD"""; Tasks: desktopicon and preset_mocvd
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\RIE"""; Tasks: desktopicon and preset_rie

[Run]
Filename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MBE"""; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; Tasks: preset_mbe
Filename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\MOCVD"""; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; Tasks: preset_mocvd
Filename: "{app}\{#AppExeName}"; Parameters: "--config-dir ""C:\DISTR\Config\Semistep\RIE"""; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent; Tasks: preset_rie

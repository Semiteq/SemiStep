; SemiStep Inno Setup installer script
; Build with: iscc.exe /DAppVersion=1.0.0 SemiStep.iss
; The AppVersion define is mandatory — pass it via the /D switch.

#ifndef AppVersion
  #error AppVersion must be defined. Pass it as: iscc.exe /DAppVersion=1.2.3 SemiStep.iss
#endif

#define AppName      "SemiStep"
#define AppPublisher "Inc Semiteq"
#define AppExeName   "UI.exe"
#define AppId        "{{8B3F2C1A-4D7E-4F9B-A2C6-1E5D8F3B7A4C}"

; Paths relative to the location of this .iss file (Installer/)
#define SrcBinDir    "..\SemiStep\Artifacts\publish\SemiStep.UI\release_win-x64"

; Configuration preset (subfolder of ConfigFiles). Pass /DConfigPreset=MBE to override.
#ifndef ConfigPreset
  #define ConfigPreset "MOCVD"
#endif
#define SrcCfgDir    "..\ConfigFiles\" + ConfigPreset
#define AppIconFile       "..\SemiStep\UI\logo.ico"
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
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Application binaries
Source: "{#SrcBinDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Configuration files — must land at the hardcoded absolute path the application reads:
;   C:\DISTR\Config\Semistep  (see Program.cs: ConfigDir constant)
Source: "{#SrcCfgDir}\actions\*";     DestDir: "C:\DISTR\Config\Semistep\actions";     Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcCfgDir}\columns\*";     DestDir: "C:\DISTR\Config\Semistep\columns";     Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcCfgDir}\connection\*";  DestDir: "C:\DISTR\Config\Semistep\connection";  Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcCfgDir}\groups\*";      DestDir: "C:\DISTR\Config\Semistep\groups";      Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcCfgDir}\properties\*";  DestDir: "C:\DISTR\Config\Semistep\properties";  Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SrcCfgDir}\ui\*";          DestDir: "C:\DISTR\Config\Semistep\ui";          Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
; Ensure the logs directory exists before the app first runs
;   C:\DISTR\Logs  (see Program.cs: LogFilePath constant)
Name: "C:\DISTR\Logs"

[Icons]
Name: "{group}\{#AppName}";                    Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";              Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

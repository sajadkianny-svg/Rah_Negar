#define ProductName "RahNegar"
#define ProductVersion "9.9.0"
#define Publisher "RahNegar"
#define SourceDir AddBackslash(SourcePath) + "publish"
#define OutputDir AddBackslash(SourcePath) + "..\Delivery\Installer"

[Setup]
AppId={{B9D8E2F7-1E1B-4C4B-9D24-990000000001}}
AppName={#ProductName}
AppVersion={#ProductVersion}
AppVerName={#ProductName} {#ProductVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\{#ProductName}
DefaultGroupName={#ProductName}
OutputDir={#OutputDir}
OutputBaseFilename=RahNegar-Setup-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#ProductName} {#ProductVersion}
SetupIconFile={#SourceDir}\AppIcon.ico
Uninstallable=yes
DisableProgramGroupPage=yes
ChangesAssociations=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{commonappdata}\{#ProductName}"
Name: "{commonappdata}\{#ProductName}\Data"; Permissions: users-modify
; Authority and transition metadata inherit the protected ProgramData ACL.
Name: "{commonappdata}\{#ProductName}\DataFiles"
Name: "{commonappdata}\{#ProductName}\Backups"; Permissions: users-modify
Name: "{commonappdata}\{#ProductName}\Logs"; Permissions: users-modify
Name: "{commonappdata}\{#ProductName}\Recovery"; Permissions: users-modify

[Icons]
Name: "{group}\{#ProductName}"; Filename: "{app}\Rah_Negar.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\Rah_Negar.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[UninstallDelete]
; Operational data under {commonappdata}\RahNegar is intentionally preserved.

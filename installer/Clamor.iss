; Inno Setup script for Clamor.
; Build with: iscc installer\Clamor.iss
; Expects a published build at ..\publish (see .github/workflows/release.yml), which
; dotnet publish produces as a self-contained win-x64 output — no .NET runtime required
; on the target machine.

#define MyAppName "Clamor"
; Overridable from the command line via: iscc /DMyAppVersion=1.2.0 Clamor.iss
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#define MyAppPublisher "Clamor"
#define MyAppExeName "Clamor.exe"

[Setup]
AppId={{92DFC252-C956-4CC1-9F31-05FA5F53722D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=ClamorSetup-{#MyAppVersion}
OutputDir=Output
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
; User data (settings.json, profiles/) lives in %AppData%\Clamor regardless of the
; installed binary's location, so nothing here needs to touch a per-user data path.

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

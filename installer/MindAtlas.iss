; MindAtlas InnoSetup Script
; Requires: Inno Setup 6+
; Build first: dotnet publish src/MindAtlas.Desktop/MindAtlas.Desktop.csproj -p:PublishProfile=win-x64

#define MyAppName "MindAtlas"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "MindAtlas"
#define MyAppExeName "MindAtlas.Desktop.exe"
#define PublishDir "..\src\MindAtlas.Desktop\bin\Release\net9.0\win-x64\publish"

[Setup]
AppId={{A7F3B2C1-D4E5-4F6A-8B9C-0D1E2F3A4B5C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\publish\installer
OutputBaseFilename=MindAtlas-{#MyAppVersion}-setup
Compression=lzma2/ultra64
SolidCompression=yes
SetupIconFile={#PublishDir}\Assets\mindatlas.ico
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start MindAtlas when Windows starts"; GroupDescription: "Startup:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

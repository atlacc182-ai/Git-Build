#define MyAppName "Git-Build"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Git-Build"
#define MyAppExeName "Git-Build.exe"

[Setup]
AppId={{C6F965EF-DC4E-44D6-B42E-E1FA1847F09B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}/Git-Build
DefaultGroupName=Git-Build
OutputDir=../outputs
OutputBaseFilename=Git-Build-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "../src/Git-Build.App/bin/Release/net8.0-windows/win-x64/publish/*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}/Git-Build"; Filename: "{app}/{#MyAppExeName}"
Name: "{autodesktop}/Git-Build"; Filename: "{app}/{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}/{#MyAppExeName}"; Description: "{cm:LaunchProgram,Git-Build}"; Flags: nowait postinstall skipifsilent

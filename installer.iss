; Inno Setup Script for SmartTaskbar++
; This script is optimized for GitHub Actions automation.

[Setup]
AppId={{959D3545-AA5C-42A8-A327-6E2C079DAA94}
AppName=SmartTaskbar++
AppVersion=1.4.4
AppPublisher=SmartTaskbar Team
DefaultDirName={autopf}\SmartTaskbar++
DefaultGroupName=SmartTaskbar++
AllowNoIcons=yes
; We output to the root so the GitHub Action can find it easily
OutputDir=.
OutputBaseFilename=SmartTaskbar_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
SetupIconFile=Sources\SmartTaskbar\Resources\Logo-White.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish_lite\SmartTaskbar++.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\SmartTaskbar++"; Filename: "{app}\SmartTaskbar++.exe"
Name: "{commondesktop}\SmartTaskbar++"; Filename: "{app}\SmartTaskbar++.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SmartTaskbar++.exe"; Description: "{cm:LaunchProgram,SmartTaskbar++}"; Flags: nowait postinstall skipifsilent

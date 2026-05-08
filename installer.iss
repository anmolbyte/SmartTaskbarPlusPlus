; Inno Setup Script for SmartTaskbar++
; Requirements: Build the portable version first using publish_portable.bat

[Setup]
AppId={{959D3545-AA5C-42A8-A327-6E2C079DAA94}
AppName=SmartTaskbar++
AppVersion=1.4.4
AppPublisher=Chanple / SmartTaskbar Team
DefaultDirName={autopf}\SmartTaskbar++
DefaultGroupName=SmartTaskbar++
AllowNoIcons=yes
; The output file of the publish script
OutputDir=setup
OutputBaseFilename=SmartTaskbar++_Setup
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

; Inno Setup Script for SmartTaskbar++
; This script is optimized for GitHub Actions automation.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif

[Setup]
; Use a unique GUID for the AppId
AppId={{959D3545-AA5C-42A8-A327-6E2C079DAA94}
AppName=SmartTaskbar++
AppVersion={#MyAppVersion}
AppPublisher=anmolbyte
; Default to Program Files
DefaultDirName={autopf}\SmartTaskbar++
DefaultGroupName=SmartTaskbar++
AllowNoIcons=yes
; Ensure the user can choose the installation directory
DisableDirPage=no
; Output the setup file to the root for the GitHub Action
OutputDir=.
OutputBaseFilename=SmartTaskbar_{#MyAppVersion}_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; Request admin privileges so it shows up correctly in Control Panel for all users
PrivilegesRequired=admin
SetupIconFile=Sources\SmartTaskbar\Resources\Logo-White.ico
UninstallDisplayIcon={app}\SmartTaskbar++.exe

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

#define MyAppName "Firmador CAL"
#define MyAppVersion "1.0.0"
#define MyAppExeName "FirmadorPades.exe"

[Setup]
AppId={{F88C2ADA-DB7D-4C00-A0EC-5F1C98B677E0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Colegio de Abogados de Lima
AppPublisherURL=https://www.cal.org.pe/v1/
DefaultDirName={autopf}\FirmaApp
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=assets\logo.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
DisableProgramGroupPage=yes
OutputDir=bin\installer
OutputBaseFilename=FirmadorCALSetup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "bin\distribution-optimized\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
Root: HKCR; Subkey: "firmaapp"; ValueType: string; ValueData: "URL:Firmador CAL Protocol"; Flags: uninsdeletekey
Root: HKCR; Subkey: "firmaapp"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCR; Subkey: "firmaapp\DefaultIcon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCR; Subkey: "firmaapp\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

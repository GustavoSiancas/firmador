#define MyAppName "Firmador CAL"
#define MyAppVersion "1.12"
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
OutputBaseFilename=FirmadorCALSetup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
AlwaysRestart=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "bin\distribution-optimized\win-x64-{#MyAppVersion}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "extra\idplug-classic-4.6.1-Peru-es-64bits.msi"; Flags: dontcopy
Source: "extra\RENIEC-DNIe.exe"; Flags: dontcopy

[Registry]
Root: HKCR; Subkey: "firmaapp"; ValueType: string; ValueData: "URL:Firmador CAL Protocol"; Flags: uninsdeletekey
Root: HKCR; Subkey: "firmaapp"; ValueType: string; ValueName: "URL Protocol"; ValueData: ""
Root: HKCR; Subkey: "firmaapp\DefaultIcon"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKCR; Subkey: "firmaapp\shell\open\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"

[Code]
var
  IDPlugInstalled, ReniecInstalled: Boolean;

function InstallDependency(const Name, Filename, Parameters: String): String;
var
  ResultCode: Integer;
begin
  Result := '';
  WizardForm.PreparingLabel.Caption := 'Instalando ' + Name + '...';
  if not Exec(Filename, Parameters, ExpandConstant('{tmp}'), SW_SHOW,
    ewWaitUntilTerminated, ResultCode) then
    Result := 'No se pudo iniciar la instalacion de ' + Name + ': ' +
      SysErrorMessage(ResultCode)
  else if (ResultCode <> 0) and (ResultCode <> 3010) then
    Result := 'La instalacion de ' + Name + ' no se completo. Codigo: ' +
      IntToStr(ResultCode) + '. Revise el registro en %TEMP%.';
  Log(Name + ': ' + IntToStr(ResultCode));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not IDPlugInstalled then
  begin
    ExtractTemporaryFile('idplug-classic-4.6.1-Peru-es-64bits.msi');
    Result := InstallDependency('IDPlug', ExpandConstant('{sys}\msiexec.exe'),
      ExpandConstant('/i "{tmp}\idplug-classic-4.6.1-Peru-es-64bits.msi" /passive /norestart REBOOT=ReallySuppress /L*v "{tmp}\FirmadorCAL-IDPlug.log"'));
    if Result <> '' then Exit;
    IDPlugInstalled := True;
  end;
  if not ReniecInstalled then
  begin
    ExtractTemporaryFile('RENIEC-DNIe.exe');
    Result := InstallDependency('RENIEC DNIe', ExpandConstant('{tmp}\RENIEC-DNIe.exe'),
      ExpandConstant('/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010 /LOG="{tmp}\FirmadorCAL-RENIEC.log"'));
    if Result <> '' then Exit;
    ReniecInstalled := True;
  end;
end;

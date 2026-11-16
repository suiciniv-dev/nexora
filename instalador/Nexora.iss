#ifndef Versao
  #define Versao "0.0.0"
#endif
#ifndef Origem
  #define Origem "..\publicado"
#endif

[Setup]
AppId={{6D2E1F4B-8C3A-4E7D-9B15-2A0C7F8E4D61}
AppName=Nexora
AppVersion={#Versao}
AppPublisher=suiciniv
VersionInfoVersion={#Versao}
DefaultDirName={autopf}\Nexora
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=saida
OutputBaseFilename=Nexora-Setup-{#Versao}
SetupIconFile=..\src\Nexora\Assets\nexora.ico
UninstallDisplayIcon={app}\Nexora.exe
UninstallDisplayName=Nexora
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
ShowLanguageDialog=auto

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ptbr.Atalho=Atalho na Área de Trabalho
en.Atalho=Desktop shortcut
ptbr.Abrir=Abrir o Nexora
en.Abrir=Open Nexora

[Tasks]
Name: "atalho"; Description: "{cm:Atalho}"

[Files]
Source: "{#Origem}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Nexora"; Filename: "{app}\Nexora.exe"; AppUserModelID: "Suiciniv.Nexora"
Name: "{autodesktop}\Nexora"; Filename: "{app}\Nexora.exe"; Tasks: atalho; AppUserModelID: "Suiciniv.Nexora"

[Run]
Filename: "{app}\Nexora.exe"; Description: "{cm:Abrir}"; Flags: nowait postinstall skipifsilent

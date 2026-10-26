#ifndef OrbeVersion
 #define OrbeVersion "1.0.0"
#endif
#ifndef OrbePayload
 #error "Execute release.ps1 para preparar o instalador."
#endif
#ifndef OrbeAppId
 #define OrbeAppId "Orbe.Desktop"
#endif
#ifndef OrbeOutput
 #define OrbeOutput "Orbe-Setup"
#endif
[Setup]
AppId={#OrbeAppId}
AppName=Orbe
AppVersion={#OrbeVersion}
AppVerName=Orbe {#OrbeVersion}
VersionInfoVersion={#OrbeVersion}.0
AppPublisher=WillianBN-Dis
AppPublisherURL=https://github.com/WillianBN-Dis/Orbe
AppSupportURL=https://github.com/WillianBN-Dis/Orbe/issues
AppUpdatesURL=https://github.com/WillianBN-Dis/Orbe/releases
DefaultDirName={localappdata}\Programs\Orbe
DefaultGroupName=Orbe
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename={#OrbeOutput}
SetupIconFile=..\assets\orbe.ico
UninstallDisplayIcon={app}\Agenda.App.exe
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
#ifdef OrbeQa
CloseApplications=no
CreateUninstallRegKey=no
#else
CloseApplications=yes
#endif
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
SetupLogging=yes
DisableProgramGroupPage=yes
[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked
[Files]
Source: "{#OrbePayload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
#ifndef OrbeQa
[Icons]
Name: "{autoprograms}\Orbe"; Filename: "{app}\Agenda.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Orbe"; Filename: "{app}\Agenda.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon
#endif
[Run]
Filename: "{app}\Agenda.App.exe"; Description: "Abrir Orbe"; Flags: nowait postinstall skipifsilent

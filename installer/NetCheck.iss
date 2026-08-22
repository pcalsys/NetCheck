#ifndef MyAppVersion
  #define MyAppVersion "1.2.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{0C3C5C18-1A43-4DD9-B103-3DFF7279C80B}
AppName=NetCheck
AppVersion={#MyAppVersion}
AppPublisher=NetCheck
AppPublisherURL=https://github.com/pcalsys/NetCheck
AppSupportURL=https://github.com/pcalsys/NetCheck/issues
DefaultDirName={autopf}\NetCheck
DefaultGroupName=NetCheck
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=NetCheck-{#MyAppVersion}-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\NetCheck.exe
LicenseFile={#SourceDir}\LICENSE.txt

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NetCheck"; Filename: "{app}\NetCheck.exe"
Name: "{autodesktop}\NetCheck"; Filename: "{app}\NetCheck.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

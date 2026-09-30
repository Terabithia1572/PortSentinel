; TerabithiaDesktop_Setup.iss temel alınarak PortSentinel için uyarlanmıştır.
; Derleme: scripts\Build-Setup.ps1. Kurulum sırasında USB politikası uygulanmaz.
#ifndef AppVersion
  #define AppVersion "1.0.2"
#endif
#ifndef PayloadDir
  #define PayloadDir SourcePath + "..\artifacts\setup-payload"
#endif
#ifndef InstallerOutputDir
  #define InstallerOutputDir SourcePath + "..\artifacts\installer"
#endif
#define AppName "PortSentinel"
#define AppPublisher "Yunus İNAN"
#define PublisherUrl "https://github.com/terabithia1572"

[Setup]
AppId={{69D4AC9F-F550-46D6-AF21-39B2394C2547}
AppName={#AppName}
AppVerName={#AppName} {#AppVersion}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#PublisherUrl}
AppSupportURL={#PublisherUrl}
AppUpdatesURL={#PublisherUrl}
AppComments=Yunus İNAN tarafından geliştirilmiştir. USB cihaz izin yönetimi ve keşif sürümü.
AppCopyright=Copyright © 2026 Yunus İNAN. Tüm hakları saklıdır.
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=PortSentinel Kurulum Programı
VersionInfoProductName={#AppName}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableDirPage=yes
DisableProgramGroupPage=no
UsePreviousAppDir=no
UsePreviousGroup=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=PortSentinel-Setup-{#AppVersion}
SetupIconFile=assets\PortSentinel.ico
UninstallDisplayIcon={app}\PortSentinel.ico
UninstallDisplayName={#AppName} {#AppVersion}
InfoBeforeFile=KURULUM-BILGISI.txt
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0
PrivilegesRequired=admin
SetupLogging=yes
CloseApplications=no
RestartApplications=no
Uninstallable=yes
UninstallFilesDir={app}

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Messages]
WelcomeLabel1=PortSentinel Kurulumuna Hoş Geldiniz
WelcomeLabel2=Bu sihirbaz PortSentinel'i bilgisayarınıza kuracaktır.%n%nGeliştirici: Yunus İNAN%nCopyright © 2026 Yunus İNAN. Tüm hakları saklıdır.%n%nBu sürüm cihaz izinlerini yönetir; fiziksel USB erişim engeli henüz doğrulanmamıştır.%n%nDevam etmeden önce diğer uygulamaları kapatmanız önerilir.
FinishedLabel=PortSentinel ve Windows servisi kuruldu.%n%nGeliştirici: Yunus İNAN%n%nFiziksel USB erişim engeli bu sürümde doğrulanmamıştır.

[Tasks]
Name: "desktopicon"; Description: "Masaüstüne kısayol oluştur"; GroupDescription: "Ek görevler:"; Flags: unchecked

[Dirs]
; Payload PrepareToInstall icinde olusturulur; bos ana dizini kaldiriciya da kaydet.
Name: "{app}"; Flags: uninsalwaysuninstall

[Files]
Source: "{#PayloadDir}\service\*"; DestDir: "{app}\service"; Flags: onlyifdoesntexist recursesubdirs createallsubdirs
Source: "{#PayloadDir}\desktop\*"; DestDir: "{app}\desktop"; Flags: onlyifdoesntexist recursesubdirs createallsubdirs
Source: "{#PayloadDir}\scripts\*"; DestDir: "{app}\scripts"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\docs\*"; DestDir: "{app}\docs"; Flags: onlyifdoesntexist recursesubdirs createallsubdirs
Source: "{#PayloadDir}\README.md"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\NOTICE.md"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\CHANGELOG.md"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\THIRD-PARTY-NOTICES.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\THIRD-PARTY-LICENSES\*"; DestDir: "{app}\THIRD-PARTY-LICENSES"; Flags: onlyifdoesntexist recursesubdirs createallsubdirs
Source: "{#PayloadDir}\PortSentinel.ico"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\installer-owner.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\checksums.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#PayloadDir}\scripts\Test-SetupEnvironment.ps1"; Flags: dontcopy
Source: "{#PayloadDir}\scripts\Undo-SetupService.ps1"; Flags: dontcopy
Source: "{#PayloadDir}\scripts\Setup.Common.ps1"; Flags: dontcopy

[Icons]
Name: "{group}\PortSentinel"; Filename: "{app}\desktop\PortSentinel.Desktop.exe"; WorkingDir: "{app}\desktop"
Name: "{group}\PortSentinel'i Kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PortSentinel"; Filename: "{app}\desktop\PortSentinel.Desktop.exe"; WorkingDir: "{app}\desktop"; Tasks: desktopicon

[Run]
Filename: "{app}\desktop\PortSentinel.Desktop.exe"; Description: "PortSentinel'i şimdi çalıştır"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
var
  ServiceRegistered: Boolean;
  InstallationCommitted: Boolean;

function RunSetupAction(Action, ScriptPath, LogPath, PackagePath: String; var Detail: String): Boolean;
var
  ExitCode: Integer;
  Index: Integer;
  Parameters: String;
  Lines: TArrayOfString;
begin
  Parameters := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ScriptPath + '"';
  if Action <> '' then Parameters := Parameters + ' -Action ' + Action;
  if PackagePath <> '' then Parameters := Parameters + ' -PackagePath "' + PackagePath + '"';
  Parameters := Parameters + ' -LogPath "' + LogPath + '"';
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'), Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  if Result then Result := ExitCode = 0;
  Detail := '';
  if FileExists(LogPath) and LoadStringsFromFile(LogPath, Lines) then begin
    for Index := 0 to GetArrayLength(Lines) - 1 do begin
      if Detail <> '' then Detail := Detail + #13#10;
      Detail := Detail + Lines[Index];
    end;
  end;
  if Detail <> '' then Log(Detail);
  if not Result and (Detail = '') then Detail := 'PowerShell işlemi çalıştırılamadı veya başarısız oldu. Kurulum günlüğünü inceleyin.';
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Detail: String;
  StagedPackage: String;
begin
  Result := '';
  if ServiceRegistered then exit;
  ExtractTemporaryFile('Test-SetupEnvironment.ps1');
  ExtractTemporaryFile('Undo-SetupService.ps1');
  ExtractTemporaryFile('Setup.Common.ps1');
  if not RunSetupAction('', ExpandConstant('{tmp}\Test-SetupEnvironment.ps1'), ExpandConstant('{tmp}\PortSentinel-preflight.log'), '', Detail) then begin
    Result := 'Kurulum ön kontrolü başarısız:' + #13#10 + Detail;
    exit;
  end;
  { Before/AfterInstall hataları Inno tarafından yutulur. Kritik kurulum bu kapıdadır. }
  { ExtractTemporaryFiles alt dizinleri ve acilmamis uygulama dizini parcasini korur. }
  ExtractTemporaryFiles('{app}\*');
  StagedPackage := ExpandConstant('{tmp}') + '\{app}';
  if not RunSetupAction('Install', StagedPackage + '\scripts\Invoke-SetupAction.ps1', ExpandConstant('{tmp}\PortSentinel-service-install.log'), StagedPackage, Detail) then begin
    Result := 'PortSentinel servisi kurulamadı. Kurulum tamamlanmadı:' + #13#10 + Detail;
    exit;
  end;
  ServiceRegistered := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then InstallationCommitted := True;
end;

procedure DeinitializeSetup;
var
  Detail: String;
begin
  { Servisten sonra kısayol/registry aşaması başarısızsa yalnız bu kurulumun servisini geri al. }
  if ServiceRegistered and not InstallationCommitted then begin
    if not RunSetupAction('', ExpandConstant('{tmp}\Undo-SetupService.ps1'), ExpandConstant('{tmp}\PortSentinel-service-rollback.log'), '', Detail) then
      Log('Servis geri alma başarısız: ' + Detail);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Detail: String;
begin
  { Bu aşama kullanıcı kaldırmayı onayladıktan sonra, dosyalar silinmeden önce çalışır. }
  if CurUninstallStep = usUninstall then begin
    if not RunSetupAction('Uninstall', ExpandConstant('{app}\scripts\Invoke-SetupAction.ps1'), ExpandConstant('{tmp}\PortSentinel-service-uninstall.log'), '', Detail) then begin
      SuppressibleMsgBox('Servis/sahiplik kontrolü başarısız. Kaldırma durduruldu, dosyalar korundu.' + #13#10 + Detail, mbError, MB_OK, IDOK);
      Abort;
    end;
  end;
end;

; =============================================================================
; Installateur Melody Paie RDC â€” Inno Setup 6
; Compiler : ISCC.exe MelodyPaieRDC.iss (ou CreerInstallateur.bat)
; =============================================================================

#define MyAppName "Melody Paie RDC"
#define MyAppVersion "1.0.5"
#define MyAppVersionShort "1.0.5"
#define MyAppPublisher "Melody Paie"
#define MyAppExeName "MelodyPaieRDC.exe"
#define MyAppCopyright "Melody Paie"
#define MyAppDescription "Paie, RH et declarations CNSS / IPR pour la RDC"

; --- Mot de passe technique d'installation (rÃ©servÃ© au fournisseur Impact Entreprises) ---
; Les clients ne doivent pas recevoir ce mot de passe : il limite la revente non autorisÃ©e de l'installateur.
; Avant livraison : remplacez par un mot de passe fort et gardez-le confidentiel.
; Alternative : ISCC /DInstallateurMotDePasseTechnique=VotreMotSecret "MelodyPaieRDC.iss"
#ifndef InstallateurMotDePasseTechnique
#define InstallateurMotDePasseTechnique "Impact2026"
#endif
; Build interne sans mot de passe (NE PAS distribuer au client) : ISCC /DBypassMotDePasseInstallation

[Setup]
; Identifiant stable (ne pas changer aprÃ¨s 1re publication : mises a jour Windows)
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersionShort}
AppPublisher={#MyAppPublisher}
AppCopyright=Copyright (C) {#MyAppCopyright}
; Remplacez par vos URL reelles avant distribution commerciale
AppPublisherURL=https://example.com/melodypaie-rdc
AppSupportURL=https://example.com/melodypaie-rdc/support
AppUpdatesURL=https://github.com/Mavuisra/melodyPaieDrc/releases
VersionInfoVersion={#MyAppVersion}
VersionInfoTextVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription={#MyAppDescription}
VersionInfoCompany={#MyAppPublisher}

DefaultDirName={code:GetInstallDir}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
DisableProgramGroupPage=no
DisableWelcomePage=no
DisableFinishedPage=no
UsePreviousAppDir=yes
UsePreviousGroup=yes

; Parcours personnalise : licence, texte d'accueil, fin
WizardStyle=modern
LicenseFile=Textes\Licence_utilisation.txt
InfoBeforeFile=Textes\Bienvenue.txt
InfoAfterFile=Textes\ApresInstallation.txt

OutputDir=..\publish\installer
OutputBaseFilename=MelodyPaieRDC_Setup_{#MyAppVersionShort}
SetupIconFile=..\Assets\Icon_MelodyPaie_Installer.ico
UninstallDisplayIcon={app}\Icon_MelodyPaie_Installer.ico
UninstallDisplayName={#MyAppName}

Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
; x64compatible : Windows x64 natif + Windows Arm64 (emulation x64) â€” recommande par Inno Setup 6.3+
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; Fermer l'app si elle tourne pendant la mise a jour
CloseApplications=yes
RestartApplications=no
AllowNetworkDrive=no
SetupLogging=yes

; Une seule langue : pas de choix inutile
ShowLanguageDialog=no

; Empecher deux assistants en parallele
SetupMutex=MelodyPaieRDC_Setup_Mutex,{#MyAppName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
; Sous-titre page d'accueil (evite accents problematiques en code Pascal)
french.CustomWelcomeSub=Cet assistant installe {#MyAppName} sur votre ordinateur.%n%nCette version inclut le runtime .NET (self-contained) : aucune installation separee du runtime n'est requise.%n%nIl est recommandÃ© de fermer l'application si elle est deja ouverte.

[Tasks]
Name: "desktopicon"; Description: "CrÃ©er un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked
Name: "autostart"; Description: "Lancer Melody Paie RDC aprÃ¨s l'installation"; GroupDescription: "AprÃ¨s l'installation :"; Flags: checkedonce

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Comment: "{#MyAppDescription}"; IconFilename: "{app}\Icon_MelodyPaie_Installer.ico"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\Icon_MelodyPaie_Installer.ico"; Tasks: desktopicon

[Run]
; WorkingDir : sans cela, Windows peut lancer l'exe avec un rÃ©pertoire courant hors {app} (ex. System32),
; ce qui casse le chargement des DLL natives / ressources Ã  cÃ´tÃ© de l'exÃ©cutable.
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent; Tasks: autostart

[Code]
#ifdef BypassMotDePasseInstallation
#else
var
  PageMotDePasse: TInputQueryWizardPage;
#endif

function GetInstallDir(Default: string): string;
var
  ExistingDir: string;
begin
  ExistingDir := '';
  if not RegQueryStringValue(HKLM64, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}_is1', 'InstallLocation', ExistingDir) then
    if not RegQueryStringValue(HKLM, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}_is1', 'InstallLocation', ExistingDir) then
      if not RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}_is1', 'InstallLocation', ExistingDir) then
        ExistingDir := '';

  if ExistingDir <> '' then
    Result := ExistingDir
  else
    Result := ExpandConstant('{autopf}\{#MyAppName}');
end;

procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel2.Caption := ExpandConstant('{cm:CustomWelcomeSub}');

#ifdef BypassMotDePasseInstallation
#else
  PageMotDePasse := CreateInputQueryPage(wpLicense,
    'Installation autorisee',
    'Reserve au deploiement par le fournisseur (Impact Entreprises).',
    'Saisissez le mot de passe technique pour continuer :');
  PageMotDePasse.Add('Mot de passe technique (fournisseur uniquement)', True);
#endif
end;

#ifndef BypassMotDePasseInstallation
function NextButtonClick(CurPageID: Integer): Boolean;
var
  MotAttendu: string;
begin
  Result := True;
  if PageMotDePasse = nil then
    Exit;
  if CurPageID <> PageMotDePasse.ID then
    Exit;

  MotAttendu := '{#InstallateurMotDePasseTechnique}';
  if (MotAttendu = '') or (MotAttendu = 'REMPLACER_AVANT_LIVRAISON') then
  begin
    MsgBox('Installation non configuree : definissez InstallateurMotDePasseTechnique dans MelodyPaieRDC.iss (ou via ISCC /D...) avant de compiler pour la livraison.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if PageMotDePasse.Values[0] <> MotAttendu then
  begin
    MsgBox('Mot de passe incorrect. Seul le fournisseur peut installer ce logiciel a partir de ce package.', mbError, MB_OK);
    Result := False;
  end;
end;
#endif

function InitializeSetup(): Boolean;
begin
  Result := False;
  if not IsWin64 then
  begin
    MsgBox('Melody Paie RDC requiert Windows en version 64 bits.', mbError, MB_OK);
    Exit;
  end;

  Result := True;
end;







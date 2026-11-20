; TallyDues-Setup.iss - Inno Setup script for TallyDues.
; Developed by OpenXpert Solutions.
;
; Installs everything to C:\ProgramData\TallyDues (no admin rights, no
; folder choice). The customer enters a license key, paid or trial, which is
; checked with the portal before anything is installed. After the files are in
; place, the keyfile is written, the portal takes the seat (one PC at a time) and
; the signed license is stored; a failed or cancelled install takes no seat,
; unless the portal's answer was lost. It then shows the one step that must be done inside
; TallyPrime (loading the add-on).
;
; All license logic is in TallyDuesLicense.exe. Build it first: see build\Build-Helper.ps1.
; BUILD: build\Build-Release.ps1 for a signed release, or the GitHub workflow for a test
; build. A test build holds placeholders, so it reports "not configured" for any key.

#define MyAppName "TallyDues"
#define MyAppVersion "1.0"
#define MyAppPublisher "OpenXpert Solutions"

; Ships the compiled add-on if one exists. Otherwise it ships the plain, readable .tdl.
#if FileExists(AddBackslash(SourcePath) + "..\TallyDues\TallyDues.tcp")
  #define AddonFile "TallyDues.tcp"
  #define AddonExclude "TallyDues.tdl"
#else
  #define AddonFile "TallyDues.tdl"
  #define AddonExclude "TallyDues.tcp"
#endif

[Setup]
AppId={{3781C9ED-2D54-407F-97D5-1CE0AF2F3FCD}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={commonappdata}\TallyDues
DefaultGroupName=TallyDues
DisableProgramGroupPage=yes
DisableWelcomePage=no
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=TallyDues-Setup
Compression=lzma
SolidCompression=yes
SetupIconFile=..\TallyDues\Scripts\branding\icon.ico
WizardStyle=modern
InfoAfterFile=next-steps.txt
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
UninstallDisplayName={#MyAppName}
ChangesEnvironment=no
AppCopyright=Developed by OpenXpert Solutions
VersionInfoVersion=1.0.0
VersionInfoCompany=OpenXpert Solutions
VersionInfoDescription=TallyDues setup - developed by OpenXpert Solutions
#ifdef Release
SignTool=tallydues
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nTallyDues shows Windows notifications for cheques (PDCs) and invoices that are due, with an on-screen dashboard inside TallyPrime.%n%nYou will need your license key and an internet connection.%n%nDeveloped by OpenXpert Solutions.%n%nIt is recommended that you close TallyPrime before continuing.

[Files]
Source: "splash.bmp"; Flags: dontcopy
Source: "..\TallyDues\TallyDuesLicense.exe"; Flags: dontcopy
Source: "..\TallyDues\*"; DestDir: "{app}"; Excludes: "license.dat,license.pending,seal.key,state.dat,license-status.json,license-notice.txt,install.log,*.tpj,{#AddonExclude}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TallyDues Folder"; Filename: "{app}"
Name: "{group}\User Guide"; Filename: "{app}\TallyDues-User-Guide.md"
Name: "{group}\Alerts - also show pop-up boxes"; Filename: "wscript.exe"; Parameters: """{app}\TDV2Toast.vbs"" TallyDues setmode both"; WorkingDir: "{app}"; Comment: "Use this if Windows hides the notifications: every alert also appears as a pop-up box"
Name: "{group}\Alerts - Windows notifications only"; Filename: "wscript.exe"; Parameters: """{app}\TDV2Toast.vbs"" TallyDues setmode toast"; WorkingDir: "{app}"; Comment: "Alerts appear only as Windows notifications"
Name: "{group}\Alerts - automatic (default)"; Filename: "wscript.exe"; Parameters: """{app}\TDV2Toast.vbs"" TallyDues setmode auto"; WorkingDir: "{app}"; Comment: "Windows notifications, plus a pop-up only when Windows is blocking them"
Name: "{group}\Change License Key"; Filename: "{app}\TallyDuesLicense.exe"; Parameters: "change-key"; WorkingDir: "{app}"; Comment: "Enter a new TallyDues license key"
Name: "{group}\Release License"; Filename: "{app}\TallyDuesLicense.exe"; Parameters: "release-window"; WorkingDir: "{app}"; Comment: "Release your TallyDues license from this PC"
Name: "{group}\Uninstall TallyDues"; Filename: "{uninstallexe}"

[Code]
const
  // Exit codes from TallyDuesLicense.exe. Keep in step with ExitCodes in Program.cs.
  CodeOk = 0;
  CodeExpired = 10;
  CodeClockBack = 12;
  CodeInvalidKey = 20;
  CodeKeyExpired = 21;
  CodeRevoked = 22;
  CodeSuspended = 23;
  CodeKeyUsed = 24;
  CodeRateLimited = 25;
  CodeNetwork = 30;
  CodeBadResponse = 31;
  CodeNotConfigured = 32;

var
  InstallOK: Boolean;
  InstallNote: String;
  KeyEntered: Boolean;
  HadLicense: Boolean;
  LicensePage: TInputQueryWizardPage;

function HelperPath: String;
begin
  ExtractTemporaryFile('TallyDuesLicense.exe');
  Result := ExpandConstant('{tmp}\TallyDuesLicense.exe');
end;

function KeyFile: String;
begin
  Result := ExpandConstant('{tmp}\key.txt');
end;

function StateFile: String;
begin
  Result := ExpandConstant('{tmp}\confirm.txt');
end;

// Runs the helper hidden and returns its exit code, or -1 if it could not start.
function RunHelper(const Params: String): Integer;
var
  Code: Integer;
begin
  Result := -1;
  try
    if Exec(HelperPath, Params, ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, Code) then
      Result := Code;
  except
    Result := -1;
  end;
end;

// An ended license still counts: the portal refuses its key until renewal, and renewing keeps that key.
function InstalledLicenseOK: Boolean;
var
  Code: Integer;
begin
  Code := RunHelper('check --install-dir "' + ExpandConstant('{commonappdata}\TallyDues') + '"');
  Result := (Code = CodeOk) or (Code = CodeExpired) or (Code = CodeClockBack);
end;

// 32 hex characters in eight groups of four.
function KeyFormatOK(const Key: String): Boolean;
var
  I: Integer;
begin
  Result := Length(Key) = 39;
  if not Result then
    Exit;
  for I := 1 to 39 do
    if I mod 5 = 0 then
    begin
      if Key[I] <> '-' then
        Result := False;
    end
    else if Pos(Key[I], '0123456789ABCDEFabcdef') = 0 then
      Result := False;
end;

function KeyMessage(Code: Integer): String;
begin
  case Code of
    CodeInvalidKey: Result := 'This license key was not found. Please check it and try again.';
    CodeKeyExpired: Result := 'This license key has expired. Please renew it on the OpenXpert portal.';
    CodeRevoked: Result := 'This license key is no longer active. Please contact OpenXpert Solutions.';
    CodeSuspended: Result := 'The subscription for this key is suspended. Please check it on the OpenXpert portal.';
    CodeKeyUsed: Result := 'This license key is active on another PC. Release it there first with TallyDues Release License, or contact OpenXpert Solutions.';
    CodeRateLimited: Result := 'Too many attempts. Please wait a while, then try again.';
    CodeBadResponse: Result := 'The answer from the license server could not be trusted. Your network may be redirecting it, for example on a public wifi login page. Please try another network, or contact OpenXpert Solutions.';
    CodeNotConfigured: Result := 'This is a test build of Setup. It cannot check license keys.';
  else
    Result := 'The license check could not run (code ' + IntToStr(Code) + '). Please contact OpenXpert Solutions.';
  end;
end;

// Asks the portal, offering Retry while it cannot be reached. Returns the last exit code.
function HelperWithRetry(const Params: String; const OfflineText: String): Integer;
begin
  repeat
    Result := RunHelper(Params);
    if Result <> CodeNetwork then
      Exit;
  until MsgBox(OfflineText, mbError, MB_RETRYCANCEL) <> IDRETRY;
end;

function CheckLicensePage: Boolean;
var
  Key: String;
  Code: Integer;
begin
  Result := False;
  KeyEntered := False;
  HadLicense := FileExists(ExpandConstant('{commonappdata}\TallyDues\license.dat'));
  Key := Uppercase(Trim(LicensePage.Values[0]));
  if Key = '' then
  begin
    Result := InstalledLicenseOK;
    if not Result then
      MsgBox('Please enter your license key.', mbError, MB_OK);
    Exit;
  end;
  if not KeyFormatOK(Key) then
  begin
    MsgBox('Please check the key: it has 32 letters and numbers in eight groups of four, like A1B2-C3D4-E5F6-0718-9A0B-1C2D-3E4F-5061.', mbError, MB_OK);
    Exit;
  end;
  if not SaveStringToFile(KeyFile, Key, False) then
  begin
    MsgBox('Setup could not save a temporary file. Please try again.', mbError, MB_OK);
    Exit;
  end;
  WizardForm.NextButton.Enabled := False;
  try
    Code := HelperWithRetry('validate --key-file "' + KeyFile + '" --state "' + StateFile + '"',
      'Setup could not reach the license server. Nothing has been installed.' + #13#10 + #13#10 +
      'Please check your internet connection, then click Retry.');
  finally
    WizardForm.NextButton.Enabled := True;
  end;
  if Code = CodeOk then
  begin
    KeyEntered := True;
    Result := True;
  end
  else if Code <> CodeNetwork then
    MsgBox(KeyMessage(Code), mbError, MB_OK);
end;

procedure InitializeWizard;
var
  Brand: TLabel;
begin
  Brand := TLabel.Create(WizardForm);
  Brand.Parent := WizardForm;
  Brand.Caption := 'Developed by OpenXpert Solutions';
  Brand.Font.Style := [fsBold];
  Brand.Left := ScaleX(20);
  Brand.Top := WizardForm.NextButton.Top + ScaleY(6);
  Brand.AutoSize := True;
  LicensePage := CreateInputQueryPage(wpWelcome,
    'License key',
    'Enter your TallyDues license key.',
    'Find your paid or 15-day trial key on the OpenXpert portal under My licenses. Setup checks it over the internet before installing anything. Each key works on one PC at a time. If TallyDues is already licensed on this PC, leave the box empty.');
  LicensePage.Add('License key:', False);
end;

procedure ShowSplash;
var
  F: TForm;
  Img: TBitmapImage;
  T: Integer;
begin
  try
    ExtractTemporaryFile('splash.bmp');
    F := TForm.Create(nil);
    try
      F.BorderStyle := bsNone;
      F.Position := poScreenCenter;
      Img := TBitmapImage.Create(F);
      Img.Parent := F;
      Img.AutoSize := True;
      Img.Bitmap.LoadFromFile(ExpandConstant('{tmp}\splash.bmp'));
      F.ClientWidth := Img.Width;
      F.ClientHeight := Img.Height;
      F.Show;
      F.Update;
      for T := 1 to 25 do
      begin
        Sleep(100);
        F.Update;
      end;
    finally
      F.Free;
    end;
  except
    // A splash is cosmetic - never let it block the install.
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = LicensePage.ID then
    Result := CheckLicensePage;
end;

function InitializeSetup: Boolean;
begin
  InstallOK := False;
  InstallNote := '';
  KeyEntered := False;
  // A silent install has no License Key page, so it can only reinstall on a licensed PC.
  if WizardSilent then
    Result := InstalledLicenseOK
  else
  begin
    ShowSplash;
    Result := True;
  end;
end;

// Writes the keyfile and has the portal take the seat. If that fails, a new install is removed.
// A PC that already had a license keeps it, because the uninstaller would delete it.
function ConfirmKey: Boolean;
var
  Code, ResultCode, Waited: Integer;
  Reason: String;
begin
  Code := HelperWithRetry('confirm --key-file "' + KeyFile + '" --state "' + StateFile + '" --install-dir "' + ExpandConstant('{app}') + '"',
    'Setup could not reach the license server to finish.' + #13#10 + #13#10 +
    'Please check your internet connection, then click Retry. If you cancel, this key is not applied. You can run Setup again on this PC with the same key.');
  Result := Code = CodeOk;
  if Result then
    Exit;
  if Code = CodeNetwork then
    Reason := 'The license server could not be reached.'
  else
    Reason := KeyMessage(Code);
  if HadLicense then
  begin
    InstallNote := Reason + #13#10 + #13#10 + 'The new key was not applied. Your existing TallyDues license is still in place.';
    DeleteFile(ExpandConstant('{app}\license.pending'));
    Exit;
  end;
  InstallNote := Reason + #13#10 + #13#10 + 'TallyDues was removed. Run Setup again with the same key. If Setup says the key is active on another PC, please contact OpenXpert Solutions.';
  Exec(ExpandConstant('{uninstallexe}'), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // The uninstaller relaunches itself from a temp copy, so Exec can return before it finishes.
  Waited := 0;
  while FileExists(ExpandConstant('{uninstallexe}')) and (Waited < 30) do
  begin
    Sleep(1000);
    Waited := Waited + 1;
  end;
end;

// The old beta lives in a different folder, so Inno does not see it as this app. Left in place,
// its add-on overrides the alert script and its notify.ps1 takes back the tallydues:// link.
function RemoveOldBeta(const OldDir: String): Boolean;
var
  ResultCode, Waited: Integer;
begin
  Exec('powershell.exe',
    '-NoProfile -ExecutionPolicy Bypass -Command "Get-ScheduledTask -TaskName ''TallyDue*'' -ErrorAction SilentlyContinue | ' +
    'Where-Object { ($_.Actions | Out-String) -match ''\\TallyDue\\'' } | Unregister-ScheduledTask -Confirm:$false"',
    ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if FileExists(OldDir + '\unins000.exe') then
  begin
    Exec(OldDir + '\unins000.exe', '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
    // The uninstaller relaunches itself from a temp copy, so Exec can return before it finishes.
    Waited := 0;
    while FileExists(OldDir + '\unins000.exe') and (Waited < 30) do
    begin
      Sleep(1000);
      Waited := Waited + 1;
    end;
  end;
  if DirExists(OldDir) then
    DelTree(OldDir, True, True, True);
  RegDeleteKeyIncludingSubkeys(HKCU, 'SOFTWARE\Classes\tallydue');
  // Install-TallyDues.ps1 registers this again for the new folder.
  RegDeleteKeyIncludingSubkeys(HKCU, 'SOFTWARE\Classes\tallydues');
  Result := not DirExists(OldDir);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  OldDir: String;
begin
  Result := '';
  OldDir := ExpandConstant('{commonappdata}\TallyDue');
  if not DirExists(OldDir) then
    Exit;
  if (not WizardSilent) and (MsgBox('An older version of TallyDues was found in ' + OldDir + '. It will be replaced.' + #13#10 + #13#10 +
      'That whole folder is deleted, including its snoozed reminders and logs. Your TallyPrime data is not touched.' + #13#10 + #13#10 +
      'Please close TallyPrime first. Continue?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) <> IDYES) then
  begin
    Result := 'The older version was left in place, so TallyDues was not installed.';
    Exit;
  end;
  if not RemoveOldBeta(OldDir) then
    Result := 'Setup could not remove the older version in ' + OldDir + '. Please close TallyPrime and run Setup again.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Ps1Path, LogPath, Args: String;
  ResultCode: Integer;
  Ran: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    Ps1Path := ExpandConstant('{app}\Install-TallyDues.ps1');
    LogPath := ExpandConstant('{app}\install.log');
    // Output of the setup script goes to install.log for diagnosis.
    Args := '-NoProfile -ExecutionPolicy Bypass -Command "& ''' + Ps1Path + ''' *> ''' + LogPath + '''; exit $LASTEXITCODE"';
    Ran := Exec('powershell.exe', Args, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if not Ran then
    begin
      InstallNote := 'Setup could not launch PowerShell to finish installing TallyDues. Your key was not used.' + #13#10 +
        'Please contact OpenXpert Solutions.';
    end
    else if ResultCode <> 0 then
    begin
      InstallNote := 'The TallyDues setup script reported an error (exit code ' + IntToStr(ResultCode) + '). Your key was not used.' + #13#10 +
        'Details are in C:\ProgramData\TallyDues\install.log - please send that file to OpenXpert Solutions.';
    end
    else if KeyEntered then
      InstallOK := ConfirmKey
    else
      InstallOK := True;
    DeleteFile(KeyFile);
    DeleteFile(StateFile);
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    if InstallOK then
    begin
      WizardForm.FinishedHeadingLabel.Caption := 'TallyDues is installed';
      WizardForm.FinishedLabel.Caption :=
        'Your license has been checked and stored. Alerts cover 7 days back and 7 days ahead; you can change that any time inside TallyPrime: Gateway of Tally > TallyDues Settings.' + #13#10 + #13#10 +
        'One last step inside TallyPrime:' + #13#10 +
        '1. Open TallyPrime.' + #13#10 +
        '2. Press F1 (Help), then TDL and Add-On, then F4 (Manage Local TDL Files).' + #13#10 +
        '3. Remove any older TallyDue file from the list. Set "Load on startup" to Yes, browse to {#AddonFile} in C:\ProgramData\TallyDues, and press Ctrl+A.' + #13#10 +
        '4. Close and reopen TallyPrime completely - a "Welcome to TallyDues" notification will appear.' + #13#10 + #13#10 +
        'Windows hiding the notifications? Use Start Menu > TallyDues > Alerts - also show pop-up boxes.' + #13#10 + #13#10 +
        'Developed by OpenXpert Solutions. Click Finish to close Setup.';
    end
    else
    begin
      WizardForm.FinishedHeadingLabel.Caption := 'TallyDues setup did not complete';
      WizardForm.FinishedLabel.Caption := InstallNote + #13#10 + #13#10 + 'Click Finish to close Setup.';
    end;
  end;
end;

procedure DeinitializeSetup;
begin
  // {tmp} is removed anyway; this also covers a wizard cancelled after the key was entered.
  DeleteFile(ExpandConstant('{tmp}\key.txt'));
  DeleteFile(ExpandConstant('{tmp}\confirm.txt'));
end;

// Frees the seat before the script below deletes the stored license, which holds the only copy of
// the key on this PC. Returns False if the user keeps TallyDues. A silent uninstall tries once.
// Setup's own rollback has no license yet, so it skips this.
function ReleaseSeat: Boolean;
var
  AppDir, Reason: String;
  Code: Integer;
begin
  Result := True;
  AppDir := ExpandConstant('{app}');
  if not FileExists(AppDir + '\license.dat') then
    Exit;
  repeat
    if not Exec(AppDir + '\TallyDuesLicense.exe', 'release --install-dir "' + AppDir + '"', AppDir, SW_HIDE, ewWaitUntilTerminated, Code) then
      Code := -1;
    if (Code = CodeOk) or UninstallSilent then
      Exit;
    if Code = CodeNetwork then
      Reason := 'TallyDues could not reach the license server to release your license.'
    else
      Reason := 'TallyDues could not release your license (code ' + IntToStr(Code) + ').';
  until MsgBox(Reason + #13#10 + #13#10 + 'Please check your internet connection, then click Retry.', mbError, MB_RETRYCANCEL) <> IDRETRY;
  Result := MsgBox('Uninstall without releasing your license?' + #13#10 + #13#10 +
    'Your key stays tied to this PC, so it cannot be used on another PC. To get it released, please call OpenXpert Solutions.' + #13#10 + #13#10 +
    'You can still reinstall on this PC with the same key.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

// Release License starts this uninstaller after the portal confirms the release, so no license.dat is left.
function InitializeUninstall: Boolean;
var
  Question: String;
begin
  if FileExists(ExpandConstant('{app}\license.dat')) then
    Question := 'Uninstalling releases your TallyDues license and deletes it from this PC. You can use the same key again, on this PC or another one.'
  else
    Question := 'There is no TallyDues license on this PC. Uninstalling removes TallyDues and cancels any pending snooze reminders.';
  Result := UninstallSilent or
    (MsgBox(Question + #13#10 + #13#10 + 'Do you want to uninstall?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES);
  if Result then
    Result := ReleaseSeat;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Ps1Path: String;
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Ps1Path := ExpandConstant('{app}\Uninstall-TallyDues.ps1');
    if FileExists(Ps1Path) then
      Exec('powershell.exe',
        '-NoProfile -ExecutionPolicy Bypass -File "' + Ps1Path + '"',
        ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;

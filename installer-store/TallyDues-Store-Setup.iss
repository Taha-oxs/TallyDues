; TallyDues-Store-Setup.iss - Inno Setup script for TallyDues (PRODUCTION, licensed).
; Developed by OpenXpert Solutions.
;
; Installs everything to C:\ProgramData\TallyDues (no admin rights, no
; folder choice), asks for the alert timeframe, runs the setup script
; silently (copies the notification engine, registers the tallydues://
; link handler for the current user, stamps the add-on file with the
; script path, currency, timeframe and the license end date) and then shows
; the one step that must be done inside TallyPrime (loading the .tdl).
; The customer chooses their signed license file (.lic) on the second page; it
; is checked before anything is installed.
;
; BUILD: compile with Inno Setup 6 on Windows, or let the included
; GitHub Actions workflow do it (see HOW-TO-GET-YOUR-EXE.txt).

#define MyAppName "TallyDues"
#define MyAppVersion "1.0"
#define MyAppPublisher "OpenXpert Solutions"

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
OutputBaseFilename=TallyDues-Store-Setup
Compression=lzma
SolidCompression=yes
SetupIconFile=..\TallyDuesStore\Scripts\branding\icon.ico
WizardStyle=modern
InfoAfterFile=next-steps.txt
WizardImageFile=wizard-large.bmp
WizardSmallImageFile=wizard-small.bmp
UninstallDisplayName={#MyAppName} ({#MyAppVersion})
ChangesEnvironment=no
AppCopyright=Developed by OpenXpert Solutions
VersionInfoCompany=OpenXpert Solutions
VersionInfoDescription=TallyDues setup - developed by OpenXpert Solutions

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nTallyDues shows Windows notifications for cheques (PDCs) and invoices that are due, with an on-screen dashboard inside TallyPrime.%n%nDeveloped by OpenXpert Solutions.%n%nIt is recommended that you close TallyPrime before continuing.

[Files]
Source: "splash.bmp"; Flags: dontcopy
Source: "..\TallyDuesStore\Install-TallyDues.ps1"; Flags: dontcopy
Source: "..\TallyDuesStore\Scripts\LicenseCommon.ps1"; Flags: dontcopy
Source: "..\TallyDuesStore\*"; DestDir: "{app}"; Excludes: "license.lic,state.dat,install.log"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TallyDues Folder"; Filename: "{app}"
Name: "{group}\User Guide (PDF)"; Filename: "{app}\TallyDues-User-Guide.pdf"
Name: "{group}\Alerts - also show pop-up boxes"; Filename: "wscript.exe"; Parameters: """{app}\TDV2Toast.vbs"" TallyDues setmode both"; WorkingDir: "{app}"; Comment: "Use this if Windows hides the notifications: every alert also appears as a pop-up box"
Name: "{group}\Alerts - Windows notifications only"; Filename: "wscript.exe"; Parameters: """{app}\TDV2Toast.vbs"" TallyDues setmode toast"; WorkingDir: "{app}"; Comment: "Alerts appear only as Windows notifications"
Name: "{group}\Alerts - automatic (default)"; Filename: "wscript.exe"; Parameters: """{app}\TDV2Toast.vbs"" TallyDues setmode auto"; WorkingDir: "{app}"; Comment: "Windows notifications, plus a pop-up only when Windows is blocking them"
Name: "{group}\Update License"; Filename: "powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File ""{app}\Scripts\Update-License.ps1"""; WorkingDir: "{app}"; Comment: "Install a renewed TallyDues license file"
Name: "{group}\Uninstall TallyDues"; Filename: "{uninstallexe}"

[Code]
var
  InstallOK: Boolean;
  InstallNote: String;
  LicChosen: String;
  LicReason: String;
  ScopePage: TInputQueryWizardPage;
  LicensePage: TInputFileWizardPage;

function InstalledLicensePath: String;
begin
  Result := ExpandConstant('{commonappdata}\TallyDues\license.lic');
end;

function ExistingLicenseHint: String;
begin
  if FileExists(InstalledLicensePath) then
    Result := ' A license is already installed on this PC: leave the box empty to keep using it.'
  else
    Result := '';
end;

// Wraps a value in single quotes for a PowerShell command line (doubles embedded quotes).
function PsQuote(const S: String): String;
var
  T: String;
begin
  T := S;
  StringChangeEx(T, '''', '''''', True);
  Result := '''' + T + '''';
end;

// Runs the install script in -ValidateOnly mode (changes nothing). True when the license is good;
// otherwise Reason holds the text the script printed.
function ValidateLicenseFile(const Path: String; var Reason: String): Boolean;
var
  OutFile, Args: String;
  ResultCode: Integer;
  Text: AnsiString;
begin
  Result := False;
  Reason := 'The license could not be checked.';
  try
    ExtractTemporaryFile('Install-TallyDues.ps1');
    ExtractTemporaryFile('LicenseCommon.ps1');
    OutFile := ExpandConstant('{tmp}\licvalidate.txt');
    DeleteFile(OutFile);
    Args := '/C powershell.exe -NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{tmp}\Install-TallyDues.ps1') +
            '" -ValidateOnly -LicenseFile "' + Path + '" > "' + OutFile + '" 2>&1';
    if Exec(ExpandConstant('{cmd}'), Args, ExpandConstant('{tmp}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      if LoadStringFromFile(OutFile, Text) then
        Reason := Trim(String(Text));
      Result := (ResultCode = 0);
    end;
  except
    Reason := 'The license could not be checked.';
  end;
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
  LicensePage := CreateInputFilePage(wpWelcome,
    'License file',
    'Choose the TallyDues license file (.lic) you received from OpenXpert Solutions.',
    'The license is checked now, before anything is installed.' + ExistingLicenseHint);
  LicensePage.Add('License file (.lic):', 'TallyDues license|*.lic|All files|*.*', '.lic');
  ScopePage := CreateInputQueryPage(LicensePage.ID,
    'Alert timeframe',
    'How many days around today should TallyDues alert you about?',
    'Enter a whole number from 0 to 365 in each box. "Back" = already due (overdue); "Ahead" = coming up. You can change these later in TallyPrime: Gateway of Tally > TallyDues Settings.');
  ScopePage.Add('Cheques - days back (already due):', False);
  ScopePage.Add('Cheques - days ahead (coming up):', False);
  ScopePage.Add('Invoices - days back (already due):', False);
  ScopePage.Add('Invoices - days ahead (coming up):', False);
  ScopePage.Values[0] := '7';
  ScopePage.Values[1] := '7';
  ScopePage.Values[2] := '7';
  ScopePage.Values[3] := '7';
end;

function ScopeValueOK(const V: String): Boolean;
var
  N: Integer;
begin
  N := StrToIntDef(Trim(V), -1);
  Result := (N >= 0) and (N <= 365);
end;

function ScopeParams: String;
begin
  Result := ' -ChqBackDays ' + IntToStr(StrToIntDef(Trim(ScopePage.Values[0]), 7)) +
            ' -ChqAheadDays ' + IntToStr(StrToIntDef(Trim(ScopePage.Values[1]), 7)) +
            ' -InvBackDays ' + IntToStr(StrToIntDef(Trim(ScopePage.Values[2]), 7)) +
            ' -InvAheadDays ' + IntToStr(StrToIntDef(Trim(ScopePage.Values[3]), 7));
end;

function ScopeValid: Boolean;
var
  K: Integer;
begin
  Result := True;
  for K := 0 to 3 do
    if not ScopeValueOK(ScopePage.Values[K]) then
      Result := False;
  if not Result then
    MsgBox('Please enter a whole number from 0 to 365 in every box.', mbError, MB_OK);
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
  begin
    LicChosen := Trim(LicensePage.Values[0]);
    if LicChosen = '' then
    begin
      if not FileExists(InstalledLicensePath) then
      begin
        MsgBox('Please choose your license file (.lic).', mbError, MB_OK);
        Result := False;
      end;
    end
    else if not FileExists(LicChosen) then
    begin
      MsgBox('That license file was not found.', mbError, MB_OK);
      Result := False;
    end
    else if not ValidateLicenseFile(LicChosen, LicReason) then
    begin
      MsgBox('This license file could not be used.' + #13#10 + #13#10 + LicReason, mbError, MB_OK);
      Result := False;
    end;
  end;
  if CurPageID = ScopePage.ID then
    Result := ScopeValid;

end;

function InitializeSetup: Boolean;
begin
  InstallOK := False;
  InstallNote := '';
  if not WizardSilent then
    ShowSplash;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Ps1Path, LogPath, Args, LicArg: String;
  ResultCode: Integer;
  Ran: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    Ps1Path := ExpandConstant('{app}\Install-TallyDues.ps1');
    LogPath := ExpandConstant('{app}\install.log');
    // Output of the setup script goes to install.log for diagnosis.
    LicArg := '';
    if LicChosen <> '' then
      LicArg := ' -LicenseFile ' + PsQuote(LicChosen);
    Args := '-NoProfile -ExecutionPolicy Bypass -Command "& ' + PsQuote(Ps1Path) + LicArg + ScopeParams + ' *> ' + PsQuote(LogPath) + '; exit $LASTEXITCODE"';
    Ran := Exec('powershell.exe', Args, ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode);
    if not Ran then
    begin
      InstallNote := 'Setup could not launch PowerShell to finish installing TallyDues.' + #13#10 +
        'Open PowerShell in C:\ProgramData\TallyDues and run Install-TallyDues.ps1 by hand.';
    end
    else if ResultCode <> 0 then
    begin
      InstallNote := 'The TallyDues setup script reported an error (exit code ' + IntToStr(ResultCode) + ').' + #13#10 +
        'Details are in C:\ProgramData\TallyDues\install.log - please send that file to OpenXpert Solutions.';
    end
    else
    begin
      InstallOK := True;
    end;
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
        'Your license has been checked and stored, and your alert timeframe is saved. You can change the timeframe any time inside TallyPrime: Gateway of Tally > TallyDues Settings.' + #13#10 + #13#10 +
        'One last step inside TallyPrime:' + #13#10 +
        '1. Open TallyPrime.' + #13#10 +
        '2. Press F1 (Help), then TDL and Add-On, then F4 (Manage Local TDL Files).' + #13#10 +
        '3. Set "Load on startup" to Yes, browse to TallyDues-Store-v1.0.tdl in C:\ProgramData\TallyDues, and press Ctrl+A.' + #13#10 +
        '4. Close and reopen TallyPrime completely - a "Welcome to TallyDues" notification will appear.' + #13#10 + #13#10 +
        'Windows hiding the notifications? Use Start Menu > TallyDues > Alerts - also show pop-up boxes.' + #13#10 +
        'Renewing? Start Menu > TallyDues > Update License.' + #13#10 + #13#10 +
        'Developed by OpenXpert Solutions. Click Finish to close Setup.';
    end
    else
    begin
      WizardForm.FinishedHeadingLabel.Caption := 'TallyDues setup did not complete';
      WizardForm.FinishedLabel.Caption := InstallNote + #13#10 + #13#10 + 'Click Finish to close Setup.';
    end;
  end;
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

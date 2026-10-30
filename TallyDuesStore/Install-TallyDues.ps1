# Install-TallyDues.ps1 - installs / renews TallyDues (PRODUCTION, licensed build).
# Developed by OpenXpert Solutions
# Normally run silently by TallyDues-Store-Setup.exe and by Start Menu > Update License.
#   -LicenseFile <path>   the customer's .lic. Without it the already-installed
#                         InstallDir\license.lic is reused (error if there is none).
#   -ValidateOnly         check the license and print "License OK - ..." (exit 0)
#                         or "ERROR: <reason>" (exit 1); changes nothing. Used by the
#                         Setup wizard's license page.
# A full run: validates the license FIRST (nothing is changed if it is bad), copies
# the engine (notify.ps1, LicenseCommon.ps1, TDV2Toast.vbs, protocol scripts,
# branding) to InstallDir (default C:\ProgramData\TallyDues), stamps the TDL next to
# this script in place (TDV2Script, TDV2Cur from the license currency, the license
# end date, and the alert timeframe), writes expiry.txt, registers the tallydues://
# link handler (HKCU), stores license.lic and writes state.dat (LASTSEEN).
# Alert timeframe: -ChqBackDays -ChqAheadDays -InvBackDays -InvAheadDays (0-365).
# When none is passed the saved timeframe is kept; a fresh TDL (TDV2ScopeRev : 1)
# gets the license WindowDays for all four.
# TDV2Cur is never left empty (one space minimum).
# Exit code 0 = success; 1 = error (message printed as "ERROR: ...").
param(
  [string]$InstallDir = "$env:ProgramData\TallyDues",
  [string]$LicenseFile = "",
  [switch]$ValidateOnly,
  [int]$ChqBackDays = 7,
  [int]$ChqAheadDays = 7,
  [int]$InvBackDays = 7,
  [int]$InvAheadDays = 7,
  [string]$TdlSource = "",
  [string]$FilesFrom = ""
)
$ErrorActionPreference = "Stop"
try {
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
# ---- shared license code: Scripts\ next to this script, next to this script, or InstallDir ----
$lc = $null
foreach ($cand in @((Join-Path (Join-Path $here "Scripts") "LicenseCommon.ps1"), (Join-Path $here "LicenseCommon.ps1"), (Join-Path $InstallDir "LicenseCommon.ps1"))) {
  if (Test-Path -LiteralPath $cand) { $lc = $cand; break }
}
if (-not $lc) { throw "LicenseCommon.ps1 was not found next to the setup script." }
. $lc

# ---- choose and check the license (before anything is changed) ----
if ($LicenseFile -eq "") {
  $inst = Join-Path $InstallDir "license.lic"
  if (Test-Path -LiteralPath $inst) { $LicenseFile = $inst }
  else { throw "No -LicenseFile was given and no installed license.lic was found. Please choose your license file." }
}
$lic = Read-TDV2LicenseFile -Path $LicenseFile
if (-not $lic.Valid) { throw ("License check failed: " + $lic.Reason) }
if ($ValidateOnly) {
  Write-Host ("License OK - " + $lic.CustomerName + " (" + $lic.OrderId + "), expires " + $lic.Expiry + ".")
  exit 0
}

# ---- validate every input so odd values cannot corrupt the TDL ----
foreach ($pair in @(@('ChqBackDays',$ChqBackDays),@('ChqAheadDays',$ChqAheadDays),@('InvBackDays',$InvBackDays),@('InvAheadDays',$InvAheadDays))) {
  if ($pair[1] -lt 0 -or $pair[1] -gt 365) { throw ($pair[0] + " must be between 0 and 365.") }
}
if ($InstallDir -match '["<>|*?]') { throw "InstallDir contains characters Windows does not allow." }
if ($lic.Currency -notmatch '^[A-Za-z0-9 .$]{1,8}$') { throw "The license currency may only contain letters, digits, space, dot or dollar (max 8 characters)." }
if ($TdlSource -eq "") { $TdlSource = (Get-ChildItem -Path $here -Filter 'TallyDues*.tdl' | Select-Object -First 1).FullName }
if (-not $TdlSource -or -not (Test-Path -LiteralPath $TdlSource)) { throw ("TDL source not found: " + $TdlSource) }
$lines = Get-Content -LiteralPath $TdlSource -Encoding UTF8
$bad = @($lines | Select-String -Pattern '[^\x00-\x7F]')
if ($bad.Count -gt 0) { throw "The TDL has non-ASCII characters - refusing to stamp it (TallyPrime would reject it)." }

# ---- copy the engine ----
if ($FilesFrom -eq "") { $cand = Join-Path $here "Scripts"; if (Test-Path -LiteralPath $cand) { $FilesFrom = $cand } else { $FilesFrom = $here } }
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
function Copy-IfDifferent([string]$From, [string]$To) {
  $a = [IO.Path]::GetFullPath($From); $b = [IO.Path]::GetFullPath($To)
  if ($a -ine $b) { Copy-Item -LiteralPath $From -Destination $To -Force }
}
foreach ($f in @("notify.ps1", "LicenseCommon.ps1", "TDV2Toast.vbs", "TallyDuesProtocol.ps1", "TallyDuesProtocol.vbs")) {
  $s = Join-Path $FilesFrom $f
  if (Test-Path -LiteralPath $s) { Copy-IfDifferent $s (Join-Path $InstallDir $f) }
  elseif ($f -in @("notify.ps1", "LicenseCommon.ps1", "TDV2Toast.vbs")) { throw ("Required file is missing from the package: " + $s) }
  else { Write-Warning ("missing, skipped: " + $s) }
}
$brandSrc = Join-Path $FilesFrom "branding"
if ((Test-Path -LiteralPath $brandSrc) -and ([IO.Path]::GetFullPath($brandSrc) -ine [IO.Path]::GetFullPath((Join-Path $InstallDir "branding")))) {
  Copy-Item -Path $brandSrc -Destination (Join-Path $InstallDir "branding") -Recurse -Force
}
$vbsShort = ""
try {
  $fso = New-Object -ComObject Scripting.FileSystemObject
  $vbsShort = $fso.GetFile((Join-Path $InstallDir "TDV2Toast.vbs")).ShortPath
} catch { $vbsShort = Join-Path $InstallDir "TDV2Toast.vbs" }
if ($vbsShort.Contains(" ")) { Write-Warning "InstallDir has spaces and 8.3 names are off - use a space-free path." }

# ---- values taken from the license ----
$inv = [Globalization.CultureInfo]::InvariantCulture
$expiryNum  = [int]$lic.Expiry
$expiryText = ([datetime]::ParseExact($lic.Expiry, "yyyyMMdd", $inv)).ToString("dd-MMM-yyyy", $inv)
$expiryNote = "Your TallyDues license expired on " + $expiryText + ". Please renew your subscription to keep receiving notifications."
$scopeRev = [long](Get-Date).ToString("yyMMddHHmm", $inv)
$licWin = [int]$lic.WindowDays
if ($licWin -gt 365) { $licWin = 365 }
$anyTf = ($PSBoundParameters.ContainsKey('ChqBackDays') -or $PSBoundParameters.ContainsKey('ChqAheadDays') -or $PSBoundParameters.ContainsKey('InvBackDays') -or $PSBoundParameters.ContainsKey('InvAheadDays'))
$fresh = (@($lines | Where-Object { $_ -match '^\s*TDV2ScopeRev\s*:\s*1\s*$' }).Count -gt 0)
$useLicWin = ((-not $anyTf) -and $fresh)
$stampScope = ($anyTf -or $useLicWin)
function Pick([string]$Name, [int]$Val) { if ($useLicWin) { return $licWin } else { return $Val } }
function Want([string]$Name) { return ($useLicWin -or $PSBoundParameters.ContainsKey($Name)) }

# ---- stamp the TDL in place ----
$out = foreach ($ln in $lines) {
  if ($ln -match '^\s*TDV2Script\s*:') { '   TDV2Script : "' + $vbsShort + '"' }
  elseif ((Want 'ChqBackDays') -and ($ln -match '^\s*TDV2DefChqBack\s*:')) { '   TDV2DefChqBack : ' + (Pick 'ChqBackDays' $ChqBackDays) }
  elseif ((Want 'ChqAheadDays') -and ($ln -match '^\s*TDV2DefChqAhead\s*:')) { '   TDV2DefChqAhead : ' + (Pick 'ChqAheadDays' $ChqAheadDays) }
  elseif ((Want 'InvBackDays') -and ($ln -match '^\s*TDV2DefInvBack\s*:')) { '   TDV2DefInvBack : ' + (Pick 'InvBackDays' $InvBackDays) }
  elseif ((Want 'InvAheadDays') -and ($ln -match '^\s*TDV2DefInvAhead\s*:')) { '   TDV2DefInvAhead : ' + (Pick 'InvAheadDays' $InvAheadDays) }
  elseif ($ln -match '^\s*TDV2Cur\s*:') { '   TDV2Cur : "' + $lic.Currency + ' "' }
  elseif ($stampScope -and ($ln -match '^\s*TDV2ScopeRev\s*:')) { '   TDV2ScopeRev : ' + $scopeRev }
  elseif ($ln -match '^\s*TDV2ExpiryNum\s*:') { '   TDV2ExpiryNum : ' + $expiryNum }
  elseif ($ln -match '^\s*TDV2ExpiryDate\s*:') { '   TDV2ExpiryDate : "' + $expiryText + '"' }
  elseif ($ln -match '^\s*TDV2ExpiryNote\s*:') { '   TDV2ExpiryNote : "' + $expiryNote + '"' }
  else { $ln }
}
if (Test-Path -LiteralPath $TdlSource) { try { Set-ItemProperty -LiteralPath $TdlSource -Name IsReadOnly -Value $false } catch { } }
[IO.File]::WriteAllLines($TdlSource, $out, [Text.Encoding]::ASCII)
# verify the stamp really landed
$check = Get-Content -LiteralPath $TdlSource -Raw
if ($check -notmatch ('TDV2ExpiryNum\s*:\s*' + $expiryNum)) { throw "Verification failed: the license end date was not written to the TDL." }
if ($check.IndexOf($vbsShort) -lt 0) { throw "Verification failed: script path was not written to the TDL." }
if ($check -match '(?m)^\s*TDV2Cur\s*:\s*""\s*$') { throw "Verification failed: TDV2Cur is empty (an empty formula is the suspected cause of Run Check failing)." }

# ---- files the bridge and the engine read ----
[IO.File]::WriteAllText((Join-Path $InstallDir "expiry.txt"), [string]$expiryNum, [Text.Encoding]::ASCII)
try {
  $proto = "HKCU:\SOFTWARE\Classes\tallydues"
  New-Item $proto -Force | Out-Null
  Set-ItemProperty $proto -Name "(Default)" -Value "URL:TallyDues Protocol"
  New-ItemProperty $proto -Name "URL Protocol" -Value "" -Force | Out-Null
  $cmd = 'powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + (Join-Path $InstallDir "TallyDuesProtocol.ps1") + '" "%1"'
  New-Item "$proto\shell\open\command" -Force | Out-Null
  Set-ItemProperty "$proto\shell\open\command" -Name "(Default)" -Value $cmd
} catch { Write-Warning ("tallydues:// link handler not registered (toast buttons will do nothing; everything else works): " + $_) }
# store the license verbatim (skipped when it already is that file) and the clock record
$licDest = Join-Path $InstallDir "license.lic"
if ([IO.Path]::GetFullPath($LicenseFile) -ine [IO.Path]::GetFullPath($licDest)) { Copy-Item -LiteralPath $LicenseFile -Destination $licDest -Force }
$todayNum = [int](Get-Date).ToString("yyyyMMdd", $inv)
$prev = 0
$stPath = Join-Path $InstallDir "state.dat"
try { if (Test-Path -LiteralPath $stPath) { foreach ($l2 in [IO.File]::ReadAllLines($stPath)) { if ($l2 -match '^\s*LASTSEEN\s*=\s*(\d{8})\s*$') { $prev = [int]$Matches[1] } } } } catch { $prev = 0 }
[IO.File]::WriteAllText($stPath, "LASTSEEN=" + [string][Math]::Max($todayNum, $prev), [Text.Encoding]::ASCII)

Write-Host "Installed scripts to $InstallDir"
Write-Host ("License OK - " + $lic.CustomerName + " (" + $lic.OrderId + "), valid until " + $expiryText + ".")
Write-Host "Updated $TdlSource in place with the correct script path."
Write-Host ("Alert timeframe (days back/ahead): " + $(if ($anyTf) { "cheques " + $ChqBackDays + "/" + $ChqAheadDays + ", invoices " + $InvBackDays + "/" + $InvAheadDays } elseif ($useLicWin) { "all four set to " + $licWin + " (from the license)" } else { "unchanged (saved settings kept)" }) + ". Change any time in Tally: Gateway > TallyDues Settings.")
Write-Host "In Tally: F1 Help > TDL and Add-On > F4 > pick $TdlSource > Ctrl+A, then RESTART Tally."
} catch {
  Write-Host ("ERROR: " + $_.Exception.Message)
  exit 1
}
exit 0

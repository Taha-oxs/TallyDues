# Install-TallyDues.ps1 - finishes a TallyDues install. Run silently by TallyDues-Setup.exe.
# Developed by OpenXpert Solutions
# Copies the engine up to InstallDir (where the add-on expects it) and registers the
# tallydues:// link handler (HKCU). The license is handled by TallyDuesLicense.exe,
# which Setup runs itself, so no license key ever reaches this script.
# Exit code 0 = success; 1 = error (message printed as "ERROR: ...").
param(
  [string]$InstallDir = "$env:ProgramData\TallyDues"
)
$ErrorActionPreference = "Stop"
try {
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

# The compiled add-on holds this path, so it cannot be changed at install time.
$expected = "C:\ProgramData\TallyDues"
if ([IO.Path]::GetFullPath($InstallDir).TrimEnd('\') -ine $expected) {
  throw ("TallyDues needs the folder " + $expected + " but this PC's shared data folder is " + $InstallDir + ". Please contact OpenXpert Solutions.")
}

# ---- copy the engine ----
$from = Join-Path $here "Scripts"
if (-not (Test-Path -LiteralPath $from)) { $from = $here }
New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
function Copy-IfDifferent([string]$From, [string]$To) {
  $a = [IO.Path]::GetFullPath($From); $b = [IO.Path]::GetFullPath($To)
  if ($a -ine $b) { Copy-Item -LiteralPath $From -Destination $To -Force }
}
foreach ($f in @("notify.ps1", "TDV2Toast.vbs", "TallyDuesProtocol.ps1", "TallyDuesProtocol.vbs")) {
  $s = Join-Path $from $f
  if (-not (Test-Path -LiteralPath $s)) { throw ("Required file is missing from the package: " + $s) }
  Copy-IfDifferent $s (Join-Path $InstallDir $f)
}
if (-not (Test-Path -LiteralPath (Join-Path $InstallDir "TallyDuesLicense.exe"))) { throw "Required file is missing from the package: TallyDuesLicense.exe" }
$brandSrc = Join-Path $from "branding"
if ((Test-Path -LiteralPath $brandSrc) -and ([IO.Path]::GetFullPath($brandSrc) -ine [IO.Path]::GetFullPath((Join-Path $InstallDir "branding")))) {
  Copy-Item -Path $brandSrc -Destination $InstallDir -Recurse -Force
}

# ---- link handler for toast buttons (same command notify.ps1 re-registers if it goes missing) ----
try {
  $proto = "HKCU:\SOFTWARE\Classes\tallydues"
  New-Item $proto -Force | Out-Null
  Set-ItemProperty $proto -Name "(Default)" -Value "URL:TallyDues Protocol"
  New-ItemProperty $proto -Name "URL Protocol" -Value "" -Force | Out-Null
  $cmd = 'wscript.exe //B //Nologo "' + (Join-Path $InstallDir "TallyDuesProtocol.vbs") + '" "%1"'
  New-Item "$proto\shell\open\command" -Force | Out-Null
  Set-ItemProperty "$proto\shell\open\command" -Name "(Default)" -Value $cmd
} catch { Write-Warning ("tallydues:// link handler not registered (toast buttons will do nothing until the next alert): " + $_) }

Write-Host "Installed scripts to $InstallDir"
Write-Host "In Tally: F1 Help > TDL and Add-On > F4 > pick the TallyDues add-on in $InstallDir > Ctrl+A, then RESTART Tally."
} catch {
  Write-Host ("ERROR: " + $_.Exception.Message)
  exit 1
}
exit 0

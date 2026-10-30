# Update-License.ps1 - Start Menu > TallyDues > Update License (PRODUCTION build).
# Developed by OpenXpert Solutions
# Lets the customer pick a new .lic file and installs it by re-running
# Install-TallyDues.ps1 -LicenseFile <file>. The license is checked BEFORE anything
# is changed, so a bad file leaves the previous license untouched. No timeframe
# parameters are passed, so the customer's saved alert timeframe is kept.
Add-Type -AssemblyName System.Windows.Forms
$ErrorActionPreference = "Stop"
function Say([string]$Text, [string]$Kind) {
  $icon = [System.Windows.Forms.MessageBoxIcon]::Information
  if ($Kind -eq "error") { $icon = [System.Windows.Forms.MessageBoxIcon]::Error }
  [void][System.Windows.Forms.MessageBox]::Show($Text, "TallyDues - Update License", [System.Windows.Forms.MessageBoxButtons]::OK, $icon)
}
try {
  $appDir = Split-Path -Parent $PSScriptRoot
  $inst = Join-Path $appDir "Install-TallyDues.ps1"
  if (-not (Test-Path -LiteralPath $inst)) {
    Say "Could not find Install-TallyDues.ps1 in $appDir. Please reinstall TallyDues." "error"
    exit 1
  }
  $dlg = New-Object System.Windows.Forms.OpenFileDialog
  $dlg.Title = "Choose your new TallyDues license file"
  $dlg.Filter = "TallyDues license (*.lic)|*.lic|All files (*.*)|*.*"
  if ($dlg.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 0 }
  $picked = $dlg.FileName
  $text = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $inst -LicenseFile $picked 2>&1 | Out-String
  $rc = $LASTEXITCODE
  if ($rc -eq 0) {
    Say "License updated. Restart TallyPrime if it is currently open." "info"
    exit 0
  }
  $first = ($text -split "`r?`n" | Where-Object { $_ -like "ERROR:*" } | Select-Object -First 1)
  if (-not $first) { $first = "The update did not complete. Details are in install.log in C:\ProgramData\TallyDues." }
  Say ($first + "`r`n`r`nYour previous license is unchanged. Please check the file, or contact OpenXpert Solutions.") "error"
  exit 1
} catch {
  Say ("The update did not complete: " + $_.Exception.Message + "`r`n`r`nYour previous license is unchanged.") "error"
  exit 1
}

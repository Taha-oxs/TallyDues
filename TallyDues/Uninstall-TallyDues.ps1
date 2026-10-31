# Uninstall-TallyDues.ps1 - reverses what Install-TallyDues.ps1 did on this
# machine: deletes the InstallDir folder (default C:\ProgramData\TallyDues),
# removes any TallyDues scheduled tasks (snooze reminders and the renewal
# check, named TallyDues-*), un-registers the tallydues:// protocol handler (HKCU)
# and removes the old TallyDues Start Menu shortcut if present. The Inno Setup
# uninstaller calls this first; the uninstaller's own exe is locked while it
# runs, so Inno removes that last. It cannot remove the add-on from
# TallyPrime's TDL list (Tally has no scripted way to do that): the user does
# that in F1 > TDL and Add-On > F4.
# The uninstaller frees the license seat before running this. Deleting the
# install folder also removes license.dat and state.dat.
param(
  [string]$InstallDir = "$env:ProgramData\TallyDues"
)
$ErrorActionPreference = "Continue"
if (Test-Path -LiteralPath $InstallDir) {
  Remove-Item -LiteralPath $InstallDir -Recurse -Force
  Write-Host "Removed $InstallDir"
} else {
  Write-Host "$InstallDir not found, nothing to remove."
}
try {
  Get-ScheduledTask -TaskName "TallyDues-*" -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$false -ErrorAction SilentlyContinue
  Write-Host "Removed TallyDues scheduled tasks."
} catch { }
$proto = "HKCU:\SOFTWARE\Classes\tallydues"
if (Test-Path $proto) {
  Remove-Item -Path $proto -Recurse -Force
  Write-Host "Removed tallydues:// protocol registration."
} else {
  Write-Host "tallydues:// protocol registration not found."
}
$shortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\TallyDues.lnk"
if (Test-Path -LiteralPath $shortcut) {
  Remove-Item -LiteralPath $shortcut -Force
  Write-Host "Removed TallyDues Start Menu shortcut (toast app identity)."
}
Write-Host "Reminder: in TallyPrime, go to F1 Help > TDL and Add-On > F4 and remove this TDL from the local TDL list, then restart Tally."

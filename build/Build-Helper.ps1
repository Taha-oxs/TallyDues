# Build-Helper.ps1 - compiles TallyDuesLicense.exe with the .NET Framework compiler built into Windows.
# Without -Release the placeholders stay, and the helper answers "not configured" to every portal call.
# -Release needs the live portal URL and public key, and refuses placeholders or plain HTTP.
# -LocalPortal is for testing on this PC only: it allows plain HTTP, and only to 127.0.0.1 or localhost.
param(
  [string]$PortalUrl = "__PORTAL_URL__",
  [string]$PublicKey = "__ED25519_PUBLIC_KEY__",
  [switch]$Release,
  [switch]$LocalPortal
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if ($Release -and $LocalPortal) { throw "A release build cannot use -LocalPortal." }
if ($Release) {
  if ($PortalUrl -notmatch '^https://[A-Za-z0-9.-]+(:\d+)?/?$') { throw "Release build needs a real https:// portal URL." }
}
if ($LocalPortal) {
  if ($PortalUrl -notmatch '^http://(127\.0\.0\.1|localhost):\d+/?$') { throw "-LocalPortal needs http://127.0.0.1:<port> or http://localhost:<port>." }
}
if ($Release -or $LocalPortal) {
  try { $keyBytes = [Convert]::FromBase64String($PublicKey) } catch { throw "The build needs the Ed25519 public key in base64." }
  if ($keyBytes.Length -ne 32) { throw "The Ed25519 public key must be 32 bytes." }
}
$defines = @()
if ($LocalPortal) { $defines += "/define:LOCAL_PORTAL" }

$temp = Join-Path ([IO.Path]::GetTempPath()) ("tallydues-build-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
  $config = Join-Path $temp "Config.cs"
  (Get-Content -LiteralPath (Join-Path $root "TallyDuesLicense\Config.cs") -Raw).Replace("__PORTAL_URL__", $PortalUrl).Replace("__ED25519_PUBLIC_KEY__", $PublicKey) |
    Set-Content -LiteralPath $config -Encoding ASCII
  $sources = @($config) + @(Get-ChildItem (Join-Path $root "TallyDuesLicense\*.cs") | Where-Object { $_.Name -ne "Config.cs" } | ForEach-Object { $_.FullName })
  $out = Join-Path $root "TallyDues\TallyDuesLicense.exe"
  & $csc /nologo /target:winexe /optimize+ /platform:anycpu "/out:$out" "/win32icon:$(Join-Path $root 'TallyDues\Scripts\branding\icon.ico')" `
    /r:System.Numerics.dll /r:System.Web.Extensions.dll /r:System.Management.dll /r:System.Security.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll $defines $sources
  if ($LASTEXITCODE -ne 0) { throw "The license helper did not compile." }
  Write-Host "Built $out"
} finally {
  Remove-Item -LiteralPath $temp -Recurse -Force
}

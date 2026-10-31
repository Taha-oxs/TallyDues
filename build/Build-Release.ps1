# Build-Release.ps1 - builds the signed TallyDues-Setup.exe on your own PC.
# It refuses to run while the portal URL or public key is missing, or with no compiled add-on.
# The certificate stays on this PC. Its password is asked for once, used only to import the
# file into your Windows certificate store for this build, and never appears on a command line.
#
#   powershell -ExecutionPolicy Bypass -File build\Build-Release.ps1 `
#     -PortalUrl https://your-portal -PublicKey <base64> -CertificatePath C:\path\to\cert.pfx
param(
  [Parameter(Mandatory = $true)][string]$PortalUrl,
  [Parameter(Mandatory = $true)][string]$PublicKey,
  [Parameter(Mandatory = $true)][string]$CertificatePath,
  [string]$TimestampUrl = "http://timestamp.digicert.com"
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "Certificate file not found: $CertificatePath" }
if (-not (Test-Path -LiteralPath (Join-Path $root "TallyDues\TallyDues.tcp"))) { throw "TallyDues\TallyDues.tcp is missing. Compile it in TallyPrime Developer first." }

$bad = Select-String -LiteralPath (Join-Path $root "TallyDues\TallyDues.tdl") -Pattern '[^\x00-\x7F]'
if ($bad) { throw "TallyDues.tdl has non-ASCII characters on line $($bad[0].LineNumber)." }

$iscc = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
if (-not (Test-Path -LiteralPath $iscc)) { throw "Inno Setup 6 is not installed: $iscc" }
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue | Sort-Object FullName | Select-Object -Last 1
if (-not $signtool) { throw "signtool.exe was not found. Install the Windows SDK signing tools." }

& (Join-Path $PSScriptRoot "Run-Tests.ps1")
& (Join-Path $PSScriptRoot "Build-Helper.ps1") -PortalUrl $PortalUrl -PublicKey $PublicKey -Release

$password = Read-Host "Certificate password" -AsSecureString
$certificate = Import-PfxCertificate -FilePath $CertificatePath -CertStoreLocation Cert:\CurrentUser\My -Password $password
try {
  $thumb = $certificate.Thumbprint
  $helper = Join-Path $root "TallyDues\TallyDuesLicense.exe"
  & $signtool.FullName sign /sha1 $thumb /fd sha256 /tr $TimestampUrl /td sha256 $helper
  if ($LASTEXITCODE -ne 0) { throw "Signing TallyDuesLicense.exe failed." }

  # Inno Setup reads $q as a quote and $f as the file to sign.
  $signCommand = '$q' + $signtool.FullName + '$q sign /sha1 ' + $thumb + ' /fd sha256 /tr ' + $TimestampUrl + ' /td sha256 $f'
  & $iscc /DRelease ("/Stallydues=" + $signCommand) (Join-Path $root "installer\TallyDues-Setup.iss")
  if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed (exit code $LASTEXITCODE)." }
} finally {
  Remove-Item -LiteralPath ("Cert:\CurrentUser\My\" + $certificate.Thumbprint) -Force
}

Write-Host "Built $(Join-Path $root 'installer\Output\TallyDues-Setup.exe')"

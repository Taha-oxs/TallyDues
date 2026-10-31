# Run-Tests.ps1 - compiles the license helper in test mode and runs tests\Tests.cs against a local mock portal.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$temp = Join-Path ([IO.Path]::GetTempPath()) ("tallydues-test-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $temp | Out-Null
try {
  $config = Join-Path $temp "Config.cs"
  (Get-Content -LiteralPath (Join-Path $root "TallyDuesLicense\Config.cs") -Raw).Replace("__PORTAL_URL__", "http://127.0.0.1:18765").Replace("__ED25519_PUBLIC_KEY__", "set-by-test") |
    Set-Content -LiteralPath $config -Encoding ASCII
  $sources = @($config) + @(Get-ChildItem (Join-Path $root "TallyDuesLicense\*.cs") | Where-Object { $_.Name -ne "Config.cs" } | ForEach-Object { $_.FullName }) + (Join-Path $root "tests\Tests.cs")
  $exe = Join-Path $temp "tests.exe"
  & $csc /nologo /target:exe /define:TEST_BUILD /main:TallyDues.License.Tests.Runner "/out:$exe" `
    /r:System.Numerics.dll /r:System.Web.Extensions.dll /r:System.Management.dll /r:System.Security.dll /r:System.Core.dll $sources
  if ($LASTEXITCODE -ne 0) { throw "The tests did not compile." }
  & $exe (Join-Path $root "tests\ed25519-vectors.txt")
  if ($LASTEXITCODE -ne 0) { throw "Tests failed." }
} finally {
  Remove-Item -LiteralPath $temp -Recurse -Force
}

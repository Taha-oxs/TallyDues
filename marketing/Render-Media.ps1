# Render-Media.ps1 - saves every asset in media.html as a PNG in marketing\output, using Edge.
$ErrorActionPreference = "Stop"
$edge = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
$out = Join-Path $PSScriptRoot "output"
New-Item -ItemType Directory -Force -Path $out | Out-Null
$page = "file:///" + ((Join-Path $PSScriptRoot "media.html") -replace '\\', '/')
$profile = Join-Path ([IO.Path]::GetTempPath()) ("tallydues-media-" + [guid]::NewGuid().ToString("N"))
$assets = [ordered]@{
  "hero" = "1920,1080"; "social-link" = "1200,630"; "social-square" = "1080,1080"
  "feature-alerts" = "1200,800"; "feature-snooze" = "1200,800"; "feature-dashboard" = "1200,800"; "feature-settings" = "1200,800"
}
try {
  foreach ($name in $assets.Keys) {
    $file = Join-Path $out "tallydues-$name.png"
    Start-Process -FilePath $edge -Wait -WindowStyle Hidden -ArgumentList @(
      "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1", "--allow-file-access-from-files",
      "--user-data-dir=`"$profile`"", "--window-size=$($assets[$name])", "--screenshot=`"$file`"", "`"$page`?a=$name`"")
    Write-Host "Saved $file"
  }
} finally {
  Remove-Item -LiteralPath $profile -Recurse -Force -ErrorAction SilentlyContinue
}

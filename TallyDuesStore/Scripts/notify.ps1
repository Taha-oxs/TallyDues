# notify.ps1 (PRODUCTION / licensed build)
# TallyDues - branded Windows desktop toast engine, HARDENED, with license gate.
# Developed by OpenXpert Solutions
#
# Usage (normally launched by TDV2Toast.vbs, never by hand):
#   powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File notify.ps1
#       -Title "..." -Message "..." -ToastType invoice|pdc|general [-Urgent] [-NoToast]
#
# Order tried: native Windows toast (built-in, no module) -> BurntToast (if installed) -> balloon tip.
# Guarantees:
#  - Bad / empty / over-long input never throws: it is corrected.
#  - Any toast failure (BurntToast missing, blocked, broken) degrades to a
#    balloon notification; if that fails too, the script exits with code 1
#    so the VBS bridge can show a pop-up box instead.
#  - Every problem is appended to toast.log beside this script.
#  - pdc / invoice toasts carry "Snooze 1 day" / "Snooze 3 days" buttons. The
#    message is saved in pending\<id>.txt (pruned after 7 days) and
#    TallyDuesProtocol.ps1 turns a click into a one-off scheduled task.
#    Any problem there only drops the snooze buttons; the toast still shows.
#  - LICENSE GATE: before anything is shown, LicenseCommon.ps1 (next to this
#    script) checks the installed license.lic (signature, end date) and
#    state.dat (clock rolled back?). Fails closed: a missing LicenseCommon.ps1
#    or any error in the check counts as "not licensed". A blocked run shows a
#    "TallyDues - License issue" notice instead of the alert.
#  - -NoToast: run the gate only (used by the bridge for pop-up-only mode).
#  Exit codes: 0 = licensed and shown (or -NoToast and licensed);
#              2 = licensed, nothing could be shown;
#              3 = license problem, notice shown; 4 = license problem, nothing shown.
#  Exit 1 is intentionally unused (it is PowerShell's own error code).

param(
    [string]$Title     = "TallyDues",
    [string]$Message   = "TallyDues ping - desktop notification pipeline OK",
    [string]$ToastType = "general",
    [switch]$Urgent,
    [switch]$NoToast
)

$ErrorActionPreference = "Stop"
$ScriptRoot = $PSScriptRoot
if ([string]::IsNullOrEmpty($ScriptRoot)) { $ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path }

function Write-TDLog([string]$Text) {
    try {
        $lf = Join-Path $ScriptRoot "toast.log"
        if ((Test-Path -LiteralPath $lf) -and ((Get-Item -LiteralPath $lf).Length -gt 200000)) { Remove-Item -LiteralPath $lf -Force }
        Add-Content -LiteralPath $lf -Value ((Get-Date -Format "yyyy-MM-dd HH:mm:ss") + "  notify.ps1: " + $Text)
    } catch { }
}

# --- Normalise input ---
if ($null -eq $Title)   { $Title = "" }
if ($null -eq $Message) { $Message = "" }
$Title   = ($Title   -replace '[\x00-\x1F]', ' ').Trim()
$Message = ($Message -replace '[\x00-\x1F]', ' ').Trim()
if ($Title.Length   -eq 0)   { $Title = "TallyDues" }
if ($Message.Length -eq 0)   { $Message = "TallyDues notification (no details were supplied)." }
if ($Title.Length   -gt 60)  { $Title   = $Title.Substring(0, 57) + "..." }
if ($Message.Length -gt 400) { $Message = $Message.Substring(0, 397) + "..." }
if (@("invoice", "pdc", "general") -notcontains $ToastType) { $ToastType = "general" }

$BrandDir   = Join-Path $ScriptRoot "branding"
$LogoPath   = Join-Path $BrandDir "logo.png"
$BannerPath = Join-Path $BrandDir "banner.png"
$IconPath   = Join-Path $BrandDir "icon.ico"

function Show-Balloon([string]$T, [string]$M) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $balloon = New-Object System.Windows.Forms.NotifyIcon
    try {
        $balloon.Icon = [System.Drawing.SystemIcons]::Information
        $balloon.BalloonTipTitle = $T
        $bm = $M
        if ($bm.Length -gt 200) { $bm = $bm.Substring(0, 197) + '...' }
        $balloon.BalloonTipText  = $bm + [Environment]::NewLine + 'Developed by OpenXpert Solutions'
        $balloon.Visible = $true
        $balloon.ShowBalloonTip(8000)
        Start-Sleep -Seconds 9
    } finally {
        $balloon.Visible = $false
        $balloon.Dispose()
    }
}

function Ensure-ProtocolHandler {
    # Self-heal: makes sure the tallydues:// link handler is registered for THIS user
    # (HKCU, no admin, no app to install). Without it Windows answers a toast-button
    # click with "Get an app to open this 'tallydues' link". Never throws.
    try {
        $hvbs = Join-Path $ScriptRoot "TallyDuesProtocol.vbs"
        $hps1 = Join-Path $ScriptRoot "TallyDuesProtocol.ps1"
        if (-not (Test-Path -LiteralPath $hps1)) { Write-TDLog "link handler script missing, registration skipped"; return }
        if (Test-Path -LiteralPath $hvbs) {
            # wscript has no console window, so a button click shows no terminal flash
            $want = 'wscript.exe //B //Nologo "' + $hvbs + '" "%1"'
        } else {
            $want = 'powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $hps1 + '" "%1"'
        }
        $proto  = "HKCU:\SOFTWARE\Classes\tallydues"
        $cmdKey = $proto + "\shell\open\command"
        $have = $null
        try { $have = (Get-Item -LiteralPath $cmdKey -ErrorAction Stop).GetValue('') } catch { }
        if ($have -ne $want) {
            New-Item $proto -Force | Out-Null
            Set-ItemProperty $proto -Name "(Default)" -Value "URL:TallyDues Protocol"
            New-ItemProperty $proto -Name "URL Protocol" -Value "" -Force | Out-Null
            New-Item $cmdKey -Force | Out-Null
            Set-ItemProperty $cmdKey -Name "(Default)" -Value $want
            Write-TDLog "registered tallydues:// link handler (was missing or different)"
        }
    } catch { Write-TDLog ("could not register tallydues:// link handler: " + $_) }
}

function Clear-ExpiredSnoozes {
    # Drops snooze-board entries (snooze.json, read by the optional Dashboard snooze
    # board) whose date has been reached. Never throws, never affects the toast.
    try {
        $path = Join-Path $ScriptRoot "snooze.json"
        if (-not (Test-Path -LiteralPath $path)) { return }
        $cur = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $today = [int](Get-Date).ToString("yyyyMMdd", [Globalization.CultureInfo]::InvariantCulture)
        $all = @($cur.Snoozes)
        $keep = @($all | Where-Object { [int]$_.UntilKey -gt $today })
        if ($keep.Count -ne $all.Count) {
            [IO.File]::WriteAllText($path, (ConvertTo-Json -InputObject @{ Snoozes = $keep } -Depth 4 -Compress), [Text.Encoding]::ASCII)
        }
    } catch { Write-TDLog ("snooze board cleanup skipped: " + $_) }
}

function Show-NativeToast([string]$T0, [string]$M0, [string]$Type) {
    # Real Windows toast through the built-in WinRT API. Needs NO extra module.
    # Uses Windows PowerShell's own app identity so Windows always accepts it.
    [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
    [void][Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]
    $appId = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'
    $t = [System.Security.SecurityElement]::Escape($T0)
    $m = [System.Security.SecurityElement]::Escape($M0)
    Ensure-ProtocolHandler
    $logoXml = ''
    if (Test-Path -LiteralPath $LogoPath) {
        $uri = ([System.Uri]$LogoPath).AbsoluteUri
        $logoXml = '<image placement="appLogoOverride" src="' + [System.Security.SecurityElement]::Escape($uri) + '"/>'
    }
    # Snooze buttons: cheque and invoice toasts only (not welcome / nothing-due / expiry).
    $snoozeXml = ''
    if ($Type -ne 'general') {
        try {
            $id = [guid]::NewGuid().ToString('N').Substring(0, 12)
            $pdir = Join-Path $ScriptRoot 'pending'
            if (-not (Test-Path -LiteralPath $pdir)) { New-Item -ItemType Directory -Path $pdir -Force | Out-Null }
            Get-ChildItem -LiteralPath $pdir -Filter '*.txt' -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } | Remove-Item -Force -ErrorAction SilentlyContinue
            [IO.File]::WriteAllText((Join-Path $pdir ($id + '.txt')), ($Type + "`r`n" + $T0 + "`r`n" + $M0), (New-Object System.Text.UTF8Encoding($false)))
            $snoozeXml = '<action content="Snooze 1 day" arguments="tallydues://snooze/1/' + $id + '" activationType="protocol"/>' +
                         '<action content="Snooze 3 days" arguments="tallydues://snooze/3/' + $id + '" activationType="protocol"/>'
        } catch { Write-TDLog ("snooze buttons skipped: " + $_) }
    }
    $xmlText = '<toast><visual><binding template="ToastGeneric">' + $logoXml +
        '<text>' + $t + '</text><text>' + $m + '</text>' +
        '<text placement="attribution">Developed by OpenXpert Solutions</text>' +
        '</binding></visual><actions>' +
        '<action content="View Details" arguments="tallydues://view/' + $Type + '" activationType="protocol"/>' +
        $snoozeXml +
        '<action content="Close" arguments="dismiss" activationType="system"/>' +
        '</actions></toast>'
    $xml = New-Object Windows.Data.Xml.Dom.XmlDocument
    $xml.LoadXml($xmlText)
    $toast = New-Object Windows.UI.Notifications.ToastNotification $xml
    [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId).Show($toast)
}

function Show-Anything([string]$T, [string]$M, [string]$Type) {
    # Native toast -> BurntToast -> balloon. Returns $true if one of them worked.
    try {
        $null = Show-NativeToast $T $M $Type
        Write-TDLog "toast shown (native)"
        return $true
    } catch { Write-TDLog ("native toast failed: " + $_) }
    try {
        Import-Module BurntToast -ErrorAction Stop
        New-BurntToastNotification -Text $T, $M, 'Developed by OpenXpert Solutions' -ErrorAction Stop
        Write-TDLog "toast shown (BurntToast)"
        return $true
    } catch { Write-TDLog ("BurntToast path failed: " + $_) }
    try {
        $null = Show-Balloon $T $M
        Write-TDLog "balloon shown"
        return $true
    } catch { Write-TDLog ("balloon fallback failed: " + $_) }
    return $false
}

Clear-ExpiredSnoozes

# --- License gate (fails closed) ---
$blockReason = ""
try {
    $lcPath = Join-Path $ScriptRoot "LicenseCommon.ps1"
    if (-not (Test-Path -LiteralPath $lcPath)) { throw "LicenseCommon.ps1 is missing." }
    . $lcPath
    $gate = Test-TDV2LicenseGate -Root $ScriptRoot
    if ($gate.Blocked) { $blockReason = [string]$gate.Reason }
} catch {
    $blockReason = "License check could not run (" + $_.Exception.Message + ") Please reinstall TallyDues or contact OpenXpert Solutions."
}
if ($blockReason -ne "") {
    Write-TDLog ("LICENSE BLOCK: " + $blockReason)
    if (Show-Anything "TallyDues - License issue" $blockReason "general") { exit 3 } else { exit 4 }
}

if ($NoToast) { Write-TDLog "licensed, -NoToast: nothing shown by design"; exit 0 }

if (Show-Anything $Title $Message $ToastType) { exit 0 } else { exit 2 }

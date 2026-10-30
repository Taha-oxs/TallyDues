# TallyDuesProtocol.ps1 - handles tallydues:// links from toast buttons.
# Registered (through TallyDuesProtocol.vbs, so no console window appears) as
# the handler for tallydues:// links.
#   tallydues://snooze/<1|3>/<id> : schedule a one-off reminder (Windows
#                                   scheduled task, current user, no admin)
#                                   that re-sends the saved message through
#                                   TDV2Toast.vbs, and record it in
#                                   snooze.json for the Dashboard snooze board.
#                                   Does NOT touch TallyPrime.
#   any other tallydues://...     : bring TallyPrime to the foreground.
# HARDENED: ignores anything that is not a tallydues:// link, never throws,
# always exits 0 (a failing click handler must never show an error).
# Usage: powershell.exe -NoProfile -ExecutionPolicy Bypass
#   -File "C:\ProgramData\TallyDues\TallyDuesProtocol.ps1" "%1"
param([string]$Uri = "")

$Inv = [Globalization.CultureInfo]::InvariantCulture

function Write-TDLog([string]$Text) {
    try {
        Add-Content -LiteralPath (Join-Path $PSScriptRoot "toast.log") -Value ((Get-Date -Format "yyyy-MM-dd HH:mm:ss") + "  TallyDuesProtocol.ps1: " + $Text)
    } catch { }
}

# The notification text names the first 5 vouchers / cheques ("(+N more)" for the rest).
# Pull those numbers out so the Dashboard can list them next to the snooze.
function Get-SnoozeItems([string]$Typ, [string]$Msg) {
    $items = @()
    $more = ""
    $marker = " due: "
    if ($Typ -eq "pdc") { $marker = " maturing: " }
    $p = $Msg.IndexOf($marker)
    if ($p -lt 0) { return "see reminder" }
    foreach ($seg in $Msg.Substring($p + $marker.Length).Split("|")) {
        $s = $seg.Trim()
        if ($s -match '^\(\+(\d+) more\)') { $more = " +" + $Matches[1] + " more"; continue }
        $tok = @($s -split '\s+')
        if ($Typ -eq "pdc") {
            if ($tok.Count -ge 2) { $items += ("Chq " + $tok[1]) }
        } elseif ($tok.Count -ge 1 -and $tok[0] -ne "") {
            $items += $tok[0]
        }
    }
    return (($items -join ", ") + $more).Trim()
}

# snooze.json = what the optional Dashboard snooze board reads. Entries whose
# date has been reached are dropped here (the TDL does no date maths).
function Update-SnoozeBoard($Entry) {
    $path = Join-Path $PSScriptRoot "snooze.json"
    $list = @()
    try {
        if (Test-Path -LiteralPath $path) {
            $cur = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
            if ($null -ne $cur -and $null -ne $cur.Snoozes) { $list = @($cur.Snoozes) }
        }
    } catch { $list = @() }
    $today = [int](Get-Date).ToString("yyyyMMdd", $Inv)
    $keep = @()
    foreach ($e in $list) {
        if ([int]$e.UntilKey -le $today) { continue }
        if ($null -ne $Entry -and $e.Kind -eq $Entry.Kind -and $e.Items -eq $Entry.Items) { continue }
        $keep += $e
    }
    if ($null -ne $Entry) { $keep += $Entry }
    $json = ConvertTo-Json -InputObject @{ Snoozes = @($keep) } -Depth 4 -Compress
    [IO.File]::WriteAllText($path, $json, [Text.Encoding]::ASCII)
}

function Invoke-Snooze([int]$Days, [string]$Id) {
    try {
        $pf = Join-Path $PSScriptRoot ("pending\" + $Id + ".txt")
        if (-not (Test-Path -LiteralPath $pf)) { throw "pending file not found (toast too old?)" }
        $l = [IO.File]::ReadAllLines($pf)
        if ($l.Count -lt 3) { throw "pending file malformed" }
        $typ = $l[0].Trim()
        if (@("pdc", "invoice") -notcontains $typ) { throw "bad type" }
        $msg = ($l[2] -replace '"', "'").Trim()
        $when = (Get-Date).AddDays($Days)
        $vbs = Join-Path $PSScriptRoot "TDV2Toast.vbs"
        $arg = '//B //Nologo "' + $vbs + '" TallyDues ' + $typ + ' Snoozed reminder: ' + $msg
        $act = New-ScheduledTaskAction -Execute "wscript.exe" -Argument $arg
        $trg = New-ScheduledTaskTrigger -Once -At $when
        $trg.EndBoundary = $when.AddDays(30).ToString("s")
        $set = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -DeleteExpiredTaskAfter (New-TimeSpan -Days 1)
        Register-ScheduledTask -TaskName ("TallyDues-Snooze-" + $Id) -Action $act -Trigger $trg -Settings $set -Force -ErrorAction Stop | Out-Null
        Write-TDLog ("snooze " + $Days + "d scheduled " + $Id + " for " + $when.ToString("yyyy-MM-dd HH:mm"))
        try {
            $kind = "Cheques"
            if ($typ -eq "invoice") { $kind = "Invoices" }
            $label = "Snoozed for 1 day"
            if ($Days -ne 1) { $label = "Snoozed for " + $Days + " days" }
            $items = (Get-SnoozeItems $typ $msg) -replace '[^A-Za-z0-9 ,+/#._-]', ''
            $cnt = 0
            $listed = $items
            if ($items -match '^(.*?)\s*\+(\d+) more$') { $listed = $Matches[1]; $cnt = [int]$Matches[2] }
            $cnt += @($listed -split ',' | Where-Object { $_.Trim() -ne '' }).Count
            $entry = [ordered]@{
                Kind     = $kind
                Items    = $items
                SnzCount = [string]$cnt
                Label    = $label
                Until    = $when.ToString("dd-MMM-yyyy", $Inv)
                UntilKey = $when.ToString("yyyyMMdd", $Inv)
            }
            Update-SnoozeBoard $entry
            Write-TDLog ("snooze board updated: " + $kind + " " + $items)
        } catch { Write-TDLog ("snooze board not updated: " + $_) }
    } catch {
        Write-TDLog ("snooze failed " + $Id + ": " + $_)
        try { [void](New-Object -ComObject WScript.Shell).Popup("TallyDues could not schedule the snooze. Details are in toast.log.", 15, "TallyDues", 48) } catch { }
    }
}

try {
    if ($Uri -notlike "tallydues://*") { exit 0 }
    if ($Uri -match '^tallydues://snooze/(1|3)/([0-9a-f]{12})/?$') {
        Invoke-Snooze ([int]$Matches[1]) $Matches[2]
        exit 0
    }
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public static class TDWin32 {
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
}
"@
    $tally = Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like "Tally*" -and $_.MainWindowHandle -ne [IntPtr]::Zero } |
        Select-Object -First 1
    if ($null -ne $tally) {
        $h = $tally.MainWindowHandle
        if ([TDWin32]::IsIconic($h)) { [void][TDWin32]::ShowWindow($h, 9) }
        [void][TDWin32]::ShowWindow($h, 5)
        [void][TDWin32]::SetForegroundWindow($h)
    }
} catch { }
exit 0

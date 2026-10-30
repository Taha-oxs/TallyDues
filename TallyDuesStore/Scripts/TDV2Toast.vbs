' TDV2Toast.vbs (PRODUCTION / licensed build) - TallyDues bridge between TallyPrime and the Windows toast.
' Developed by OpenXpert Solutions
' Launched by the TDL:  wscript.exe //B //Nologo TDV2Toast.vbs <Title> <Type> <Message words...>
'
' HARDENED: this script must never show a script-error dialog and never
' block TallyPrime, whatever Tally passes in (missing arguments, quotes,
' percent signs, backslashes, pipes, very long or non-English names...).
'  1. Main mode: sanitise the text, then hand off to a detached "worker"
'     copy of this script and exit immediately (Tally never waits).
'  2. Worker mode: always run notify.ps1 (the license gate lives there) and wait.
'     Exit codes: 0 shown, 2 licensed but nothing shown, 3/4 license problem,
'     anything else = engine could not run. Every failure ends in a
'     self-closing pop-up box so the user is still told. Problems are written
'     to toast.log next to this file.
On Error Resume Next

Dim fso, sh, base, n, i, mode
Set fso = CreateObject("Scripting.FileSystemObject")
Set sh = CreateObject("WScript.Shell")
If fso Is Nothing Or sh Is Nothing Then WScript.Quit 0
base = fso.GetParentFolderName(WScript.ScriptFullName)

Sub LogLine(txt)
  On Error Resume Next
  Dim lf, f
  lf = base & "\toast.log"
  If fso.FileExists(lf) Then
    If fso.GetFile(lf).Size > 200000 Then fso.DeleteFile lf, True
  End If
  Set f = fso.OpenTextFile(lf, 8, True)
  f.WriteLine Now & "  TDV2Toast.vbs: " & txt
  f.Close
End Sub

' Reads a registry value, returns "" if missing.
Function RegVal(ByVal path)
  On Error Resume Next
  Dim v
  RegVal = ""
  Err.Clear
  v = sh.RegRead(path)
  If Err.Number = 0 Then RegVal = CStr(v)
  Err.Clear
End Function

' Returns "" if Windows does not seem to block toasts, otherwise a plain-English reason.
Function ToastBlockReason()
  On Error Resume Next
  Dim r
  r = ""
  If RegVal("HKCU\Software\Microsoft\Windows\CurrentVersion\PushNotifications\ToastEnabled") = "0" Then
    r = "Windows setting 'Get notifications from apps and other senders' is OFF (Settings > System > Notifications)."
  ElseIf RegVal("HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\NOC_GLOBAL_SETTING_TOASTS_ENABLED") = "0" Then
    r = "Windows notifications are switched off for this user (Settings > System > Notifications)."
  ElseIf RegVal("HKCU\SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications\NoToastApplicationNotification") = "1" Or RegVal("HKLM\SOFTWARE\Policies\Microsoft\Windows\CurrentVersion\PushNotifications\NoToastApplicationNotification") = "1" Then
    r = "A Windows policy (set by your IT / administrator) blocks app notifications."
  End If
  ToastBlockReason = r
End Function

' notify-mode.txt next to this script: toast | popup | both | auto (default).
Function ReadMode()
  On Error Resume Next
  Dim mf, t
  ReadMode = "auto"
  mf = base & "\notify-mode.txt"
  If fso.FileExists(mf) Then
    Set t = fso.OpenTextFile(mf, 1, False)
    ReadMode = LCase(Trim(Replace(Replace(t.ReadAll, vbCr, ""), vbLf, "")))
    t.Close
  End If
  If ReadMode <> "toast" And ReadMode <> "popup" And ReadMode <> "both" Then ReadMode = "auto"
End Function


' Make arbitrary text safe to put inside a quoted command-line argument.
Function Clean(ByVal s, ByVal maxLen)
  On Error Resume Next
  Dim k, o
  o = CStr(s)
  For k = 1 To 31
    o = Replace(o, Chr(k), " ")
  Next
  o = Replace(o, Chr(127), " ")
  o = Replace(o, Chr(34), "'")
  o = Replace(o, "\", "/")
  o = Replace(o, "%", " percent")
  o = Replace(o, "`", "'")
  o = Trim(o)
  Do While Left(o, 1) = "-"
    o = Trim(Mid(o, 2))
  Loop
  If Len(o) > maxLen Then o = Left(o, maxLen - 3) & "..."
  Clean = o
End Function

n = WScript.Arguments.Count
If n < 1 Then WScript.Quit 0
mode = LCase(WScript.Arguments(0))

Dim title, typ, msg
LogLine "invoked: mode=" & mode & " args=" & n

If mode = "--worker" Then
  ' ---------------- worker: args = --worker title type message -------------
  title = "TallyDues": typ = "general": msg = "TallyDues notification"
  If n >= 2 Then title = WScript.Arguments(1)
  If n >= 3 Then typ = WScript.Arguments(2)
  If n >= 4 Then msg = WScript.Arguments(3)

  Dim ps1, cmd, rc, nmode, showPopup, popMsg
  nmode = ReadMode()
  showPopup = False
  popMsg = msg
  rc = -1
  ps1 = base & "\notify.ps1"
  If fso.FileExists(ps1) Then
    cmd = "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File " & Chr(34) & ps1 & Chr(34) & _
          " -Title " & Chr(34) & title & Chr(34) & " -Message " & Chr(34) & msg & Chr(34) & " -ToastType " & Chr(34) & typ & Chr(34)
    If nmode = "popup" Then cmd = cmd & " -NoToast"
    Err.Clear
    rc = sh.Run(cmd, 0, True)
    If Err.Number <> 0 Then rc = -1
    LogLine "notify.ps1 exit code " & rc
  Else
    LogLine "notify.ps1 not found at " & ps1
  End If
  Select Case rc
    Case 0
      If nmode = "popup" Or nmode = "both" Then showPopup = True
      If nmode = "auto" And ToastBlockReason() <> "" Then showPopup = True: LogLine "auto: Windows blocks toasts, showing pop-up too"
    Case 2
      showPopup = True
    Case 3, 4
      showPopup = True
      popMsg = "TallyDues could not verify a valid license. Use Start Menu > TallyDues > Update License, or contact OpenXpert Solutions."
    Case Else
      showPopup = True
      popMsg = "TallyDues could not start its notification engine, so no alerts were shown. Please reinstall TallyDues or contact OpenXpert Solutions."
  End Select
  If showPopup Then
    ' pop-up box: works without PowerShell and without notifications; closes itself after 25 seconds
    sh.Popup popMsg & vbCrLf & vbCrLf & "Developed by OpenXpert Solutions", 25, title, 64
  End If
  WScript.Quit 0
End If

' ---------------- main: args = Title Type Message words... ----------------
' Join ALL arguments and split them ourselves, so it does not matter whether
' TallyPrime hands over many words, or one quoted block, or something in between.
Dim raw, k2, p
raw = ""
For k2 = 0 To n - 1
  If raw <> "" Then raw = raw & " "
  raw = raw & WScript.Arguments(k2)
Next
raw = Trim(raw)
LogLine "raw args (" & n & " args, " & Len(raw) & " chars): " & Left(Replace(Replace(raw, vbCr, " "), vbLf, " "), 200)
Dim rest
rest = ""
For k2 = 0 To n - 1
  If rest <> "" Then rest = rest & " "
  rest = rest & WScript.Arguments(k2)
Next
rest = Trim(rest)
p = InStr(rest, " ")
If p > 0 Then
  title = Left(rest, p - 1): rest = Trim(Mid(rest, p + 1))
Else
  title = rest: rest = ""
End If
p = InStr(rest, " ")
If p > 0 Then
  typ = LCase(Left(rest, p - 1)): msg = Trim(Mid(rest, p + 1))
Else
  typ = LCase(rest): msg = ""
End If
title = Clean(title, 60)
If title = "" Then title = "TallyDues"
typ = Clean(typ, 20)
If typ <> "invoice" And typ <> "pdc" And typ <> "general" And typ <> "setmode" Then
  If msg = "" Then msg = typ Else msg = typ & " " & msg
  typ = "general"
End If
msg = Clean(msg, 400)
If msg = "" Then msg = "TallyDues notification (no details were supplied)."

' License end date: expiry.txt (yyyymmdd, written by Install-TallyDues.ps1 from the license) checked against the Windows clock. First of two layers: notify.ps1 re-checks the signed license.
Dim expNum, todayNum, expTxt
expNum = 0
Err.Clear
If fso.FileExists(base & "\expiry.txt") Then
  Set f = fso.OpenTextFile(base & "\expiry.txt", 1, False)
  expNum = CLng(Trim(f.ReadAll))
  f.Close
End If
If Err.Number <> 0 Then expNum = 0
Err.Clear
todayNum = Year(Now) * 10000 + Month(Now) * 100 + Day(Now)
If expNum > 0 And todayNum > expNum And typ <> "setmode" Then
  expTxt = Right(CStr(expNum), 2) & "/" & Mid(CStr(expNum), 5, 2) & "/" & Left(CStr(expNum), 4)
  typ = "general"
  msg = "Your TallyDues license expired on " & expTxt & ". Please renew your subscription to keep receiving notifications."
  LogLine "license end date passed (" & expNum & ")"
End If

If typ = "setmode" Then
  Dim wanted
  wanted = LCase(Trim(msg))
  If wanted <> "toast" And wanted <> "popup" And wanted <> "both" And wanted <> "auto" Then wanted = "auto"
  Err.Clear
  Set f = fso.OpenTextFile(base & "\notify-mode.txt", 2, True)
  f.Write wanted
  f.Close
  If Err.Number = 0 Then
    sh.Popup "TallyDues alert style is now: " & wanted & vbCrLf & vbCrLf & "toast = Windows notification only" & vbCrLf & "both  = Windows notification AND a pop-up box" & vbCrLf & "popup = pop-up box only" & vbCrLf & "auto  = notification, plus a pop-up if Windows is blocking notifications" & vbCrLf & vbCrLf & "Developed by OpenXpert Solutions", 20, "TallyDues", 64
  Else
    sh.Popup "Could not save the setting: " & Err.Description, 20, "TallyDues", 48
  End If
  WScript.Quit 0
End If

' de-duplicate: same message within 20 seconds (e.g. key held down / double click)
Dim mark, f, last, tooSoon
mark = base & "\last-toast.txt"
tooSoon = False
If typ = "setmode" Then
  tooSoon = False
ElseIf fso.FileExists(mark) Then
  Set f = fso.OpenTextFile(mark, 1, False)
  last = f.ReadAll
  f.Close
  If last = msg Then
    If DateDiff("s", fso.GetFile(mark).DateLastModified, Now) < 20 Then tooSoon = True
  End If
End If
If tooSoon Then LogLine "skipped: same message within 20 seconds": WScript.Quit 0
Err.Clear
Set f = fso.OpenTextFile(mark, 2, True)
f.Write msg
f.Close
Err.Clear

sh.Run "wscript.exe //B //Nologo " & Chr(34) & WScript.ScriptFullName & Chr(34) & " --worker " & _
       Chr(34) & title & Chr(34) & " " & Chr(34) & typ & Chr(34) & " " & Chr(34) & msg & Chr(34), 0, False
If Err.Number <> 0 Then LogLine "could not start worker: " & Err.Description Else LogLine "worker started: type=" & typ & " msg=" & Left(msg, 120)
WScript.Quit 0

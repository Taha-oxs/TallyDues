' TallyDuesProtocol.vbs - registered handler for tallydues:// links (toast buttons).
' Developed by OpenXpert Solutions
' wscript.exe has no console window, so a button click no longer flashes a
' terminal. This script starts TallyDuesProtocol.ps1 completely hidden
' (window style 0) and returns at once. It ignores anything that is not a
' tallydues:// link and never shows an error.
On Error Resume Next
Dim sh, fso, base, uri, cmd
Set sh = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
If sh Is Nothing Or fso Is Nothing Then WScript.Quit 0
base = fso.GetParentFolderName(WScript.ScriptFullName)
uri = ""
If WScript.Arguments.Count > 0 Then uri = WScript.Arguments(0)
If LCase(Left(uri, 12)) <> "tallydues://" Then WScript.Quit 0
uri = Replace(uri, Chr(34), "")
cmd = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File " & Chr(34) & base & "\TallyDuesProtocol.ps1" & Chr(34) & " " & Chr(34) & uri & Chr(34)
sh.Run cmd, 0, False
WScript.Quit 0

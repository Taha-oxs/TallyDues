---
name: release-security-review
description: Pre-release security review of the TallyDue TDL, PowerShell, VBScript and installer files for hardcoded URLs or secrets, unvalidated input and weak tamper protection. Use before any public release or when asked to security-review this project. Reviews the whole package, whereas the built-in security-review only covers pending changes on a branch.
---

# Release security review

Read-only unless the user asks for fixes. Review the files that will really ship: work out the package list from the installer first, and check `Scripts/` as well as the root, since the copies differ.

## 1. Secrets and hardcoded values

Grep shipped text files for `https?://`, `api[_-]?key`, `secret`, `token`, `password`, `BEGIN .*PRIVATE`, and developer paths or machine names. Every URL must be expected and HTTPS. Only public keys may ship.

## 2. Untrusted input

Ledger names, voucher numbers and narrations come from customer data, and `tallydue://` links can be triggered by any web page or document.

- **TDL to script:** `TDV2RunCheck` builds a command line from `$LedgerName` and `$PartyLedgerName` and passes it through `Browse URL` to `wscript.exe`. Test names containing `"`, `&`, `|`, `%`, `^` and `$(...)`, and confirm `Clean()` in `TDV2Toast.vbs` covers them.
- **Link handlers:** the handlers must accept only strict patterns. Check that the old `TallyDueProtocol.ps1` is as strict as the `tallydues://` one, whose snooze regex is the standard to meet.
- **Toast XML:** escape all message text before it goes in toast XML in `notify.ps1`.
- **Scheduled tasks:** `Invoke-Snooze` puts saved message text in task arguments, and only removes `"`. Test with the characters above.
- **Files:** `pending\<id>.txt` and `snooze.json` can be edited by a local user, so treat them as untrusted. Check `[int]` conversions on them cannot break the flow.

## 3. Local privileges

- Check who can write to the install folder (`icacls`). Scripts in `C:\ProgramData` that a less-privileged user can change are a privilege risk if another user runs them.
- `-ExecutionPolicy Bypass` is used by the link handler, so that handler's script must not be writable by others.

## 4. Tamper protection

Apply the `license-security` skill and report which of its current gaps remain.

## 5. Data exposure

`toast.log`, `pending\` and `last-toast.txt` hold customer names and amounts. Check the logs do not leak keys, that old files get cleaned up (42 pending files had built up in `C:ProgramDataTallyDuepending` by 2026-10-30), and that docs' privacy claims are still true once a license server exists.

## 6. Package contents

The package must not contain logs, `pending\`, `snooze.json`, dumps, test data, `.claude\`, or old TDL versions. Setup.exe and the uninstaller must be signed.

## Report

List findings most severe first, each with `file:line`, a concrete way to trigger it, and a fix. Keep confirmed issues apart from suspected ones, and say what could not be tested. End with a clear ship or do-not-ship call.

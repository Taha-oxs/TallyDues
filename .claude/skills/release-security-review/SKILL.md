---
name: release-security-review
description: Pre-release security review of the TallyDues TDL, PowerShell, VBScript, C# and installer files for hardcoded URLs or secrets, unvalidated input and weak tamper protection. Use before any public release or when asked to security-review this project. Reviews the whole package, whereas the built-in security-review only covers pending changes on a branch.
---

# Release security review

Read-only unless the user asks for fixes. Review the files that will really ship: work out the package list from the `.iss` first.

## 1. Secrets and hardcoded values

Grep shipped text files for `https?://`, `api[_-]?key`, `secret`, `token`, `password`, `BEGIN .*PRIVATE`, and developer paths or machine names. Every URL must be expected and HTTPS. Only public keys may ship. Check the git history too.

## 2. Untrusted input

Ledger names, voucher numbers and narrations come from customer data, and `tallydues://` links can be triggered by any web page or document.

- **TDL to script:** `TDV2RunCheck` builds a command line from `$LedgerName` and `$PartyLedgerName` and passes it through `Browse URL` to `wscript.exe`. Test names containing `"`, `&`, `|`, `%`, `^` and `$(...)`, and confirm `Clean()` in `TDV2Toast.vbs` covers them.
- **Link handler:** `TallyDuesProtocol.ps1` must accept only strict patterns. `tallydues://renew` must take nothing from the link.
- **Toast XML:** escape all message text before it goes in toast XML in `notify.ps1`.
- **Portal answers:** nothing is trusted until its signature and nonce pass.
- **Files:** `pending\`, `snooze.json`, `state.dat` and `license.dat` can be edited by a local user, so treat them as untrusted.

## 3. Local privileges

- Check who can write to the install folder (`icacls`). Scripts in `C:\ProgramData` that a less-privileged user can change are a privilege risk if another user runs them.
- `-ExecutionPolicy Bypass` is used by the link handler, so that handler's script must not be writable by others.

## 4. Tamper protection

Apply the `license-security` skill and report what still depends on trusting the client.

## 5. Data exposure

`pending\` and `snooze.json` hold customer names and amounts. Confirm that `toast.log`, `last-toast.txt` and scheduled task arguments hold no customer text and no license key, that old files get cleaned up, and that the docs' privacy claims match what goes to the portal.

## 6. Package contents

The package must not contain logs, `pending\`, `snooze.json`, dumps, test data, `.claude\`, `tests\`, or a test build of the helper. Setup, the uninstaller and `TallyDuesLicense.exe` must be signed.

## Report

List findings most severe first, each with `file:line`, a concrete way to trigger it, and a fix. Keep confirmed issues apart from suspected ones, and say what could not be tested. End with a clear ship or do-not-ship call.

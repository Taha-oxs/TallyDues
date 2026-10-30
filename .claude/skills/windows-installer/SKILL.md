---
name: windows-installer
description: Compare installer options and design or change the TallyDue Windows setup, including the custom License Key page, Inno Setup scripts, code signing, upgrades and uninstall. Use before building or changing anything related to Setup.exe, Install-TallyDue.ps1 or the uninstall scripts.
---

# Windows installer

Compare options before building. Default to Inno Setup unless a requirement rules it out.

| Option | License Key page | Fit |
|---|---|---|
| **Inno Setup** | Built in: `CreateInputQueryPage`, or `UserInfoPage` with `CheckSerial` | Best fit. The shipped uninstaller (`unins000.exe`) is already Inno. |
| WiX / MSI | Custom dialog authoring, much more work | Only if a customer needs MSI or Group Policy deployment |
| NSIS | nsDialogs custom page | Workable, but no benefit over Inno here |
| PowerShell only | Hand-built WinForms | Avoid: SmartScreen, execution policy and no real uninstall entry |

## Before starting

- The `.iss` source is **not** in this folder and the Inno compiler was not found on this PC. Ask for the source; do not rebuild it by guesswork.
- Treat `Install-TallyDue.ps1` as the install logic that Setup runs silently.

## Design rules

- Validate the key format on the page, and verify the signed server response in the same code the app uses. See the `license-security` skill. The installer is never the only license check.
- Handle no internet, server errors and bad keys with distinct messages. Never leave a half-finished install.
- Pass the key to scripts by a temp file with restricted access, not on the command line, where it can be seen and logged.
- Do not store API secrets in `[Code]`: Inno scripts can be extracted from a Setup.exe.
- Sign both Setup.exe and the uninstaller with `SignTool`, or SmartScreen will warn customers.
- Set `AppId` once and never change it, so upgrades replace the old install.

## Pitfalls in this project

- **Elevation:** `HKCU` protocol registration lands in the wrong profile if an admin elevates from another account. Test this case, or register per machine.
- **Wrong TDL:** `Install-TallyDue.ps1` picks the first `TallyDue*.tdl`, so with several versions in the folder it can stamp an old one. Pass `-TdlSource` explicitly.
- **Mixed names:** `TallyDue` and `TallyDues` coexist across files, folders and link handlers (`tallydue://` and `tallydues://`). `Install-TallyDue.ps1` registers only the old one. Upgrades must clean up both.
- **Stamping:** it rewrites the TDL in place, which cannot work once the TDL is compiled. Move stamped values to a config file the TDL reads.
- **Uninstall:** the scripts delete `InstallDir`, so user data such as `snooze.json` goes too. Source lives in this repo; only test installs go in `C:ProgramDataTallyDue`, so never make edits there.
- **Reinstall:** every install starts a fresh trial (see `license-security`).

## Definition of done

Test fresh install, upgrade over each older name, repair, uninstall, a standard user, an elevated admin from another account, offline, and a path with spaces. Add results to the `test-matrix` skill's run log.

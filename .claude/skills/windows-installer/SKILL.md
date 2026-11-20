---
name: windows-installer
description: Design or change the TallyDues Windows setup, including the License Key page, Inno Setup script, code signing, upgrades and uninstall. Use before building or changing anything related to Setup.exe, Install-TallyDues.ps1 or the uninstall scripts.
---

# Windows installer

Setup is Inno Setup: `installer\TallyDues-Setup.iss`. `TallyDues\Install-TallyDues.ps1` is the logic Setup runs silently after copying files.

## Builds

- **Test:** `.github\workflows\build.yml`, unsigned, with placeholders. It cannot activate a key.
- **Release:** `build\Build-Release.ps1` on the user's PC, because the certificate never goes to GitHub. See `TallyDues-Build-and-Release-Guide.md`.
- Inno Setup is not installed on this PC, so the `.iss` can only be compiled by the workflow, or after the user installs it.

## Design rules

- The License Key page checks format locally, then runs the helper. See the `license-security` skill. Setup holds no license logic beyond mapping exit codes to messages.
- Handle no internet, server errors and bad keys with distinct messages. Never leave a half-finished install: a cancelled `confirm` runs the uninstaller.
- Pass the key to the helper by a temp file, not on the command line.
- Do not store secrets in `[Code]`: Inno scripts can be extracted from a Setup.exe.
- Sign Setup, the uninstaller and `TallyDuesLicense.exe`.
- Keep `AppId` unchanged, so reinstalls replace the old install.
- Nothing is stamped into the add-on, because the `.tcp` cannot be changed. Anything per-install goes in a file the TDL reads, like `license-status.json`.

## Pitfalls in this project

- **Elevation:** `HKCU` protocol registration and DPAPI are per user, so an admin elevating from another account breaks them. Multiple accounts are unsupported in the first release.
- **Fixed path:** the add-on holds `C:\ProgramData\TallyDues\TDV2Toast.vbs`, so `Install-TallyDues.ps1` refuses any other folder.
- **Uninstall:** the script deletes `InstallDir`, so `snooze.json` goes too. The uninstaller warns first and releases the seat before the script runs, because the stored key is deleted with the folder.
- **TDL list:** Setup cannot add or remove the add-on in TallyPrime's F4 list, so the user does that.

## Definition of done

Test fresh install, reinstall, cancelled and failed installs, uninstall, a standard user, and offline. Add results to the `test-matrix` skill's run log.

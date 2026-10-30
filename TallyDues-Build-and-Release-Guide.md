---
title: "TallyDues - Build and Release Guide"
subtitle: "How to turn the packages into Setup programs and release them"
author: "OpenXpert Solutions"
date: "22 October 2026"
---

# TallyDues - Build and Release Guide

**Audience:** the maintainer at OpenXpert Solutions.\
**Covers:** the Public Beta installer (`TallyDues-PublicBeta-Setup.exe`) and the Production installer (`TallyDues-Store-Setup.exe`).\
**Companion documents:** *TallyDues - License Generator Guide*, *TallyDues - System Design*, *TallyDues - Code Documentation*

> **Status.** The Setup programs are built on Windows by an Inno Setup compiler, which is not available where these packages were prepared. The two installer scripts and the GitHub workflows have therefore **never been compiled by the preparer**. Compiling them is your first test (section 3.4). Everything else in the packages was parse-checked and the install scripts were run on Linux PowerShell 7.\
> **This folder** is the complete Production (licensed) repository: workflow, installer with its `.bmp` images, add-on and guides. `dev-tools` (which creates licenses) is delivered separately and must never be added to the repository.\
> **Signing key.** This Production package was rebuilt from the written specification, because the original Store files were not available. It carries a **new** signing key (section 2, step 1). Licenses signed with an older key will not verify with it.

## 1. What you have

| File | Contains | Output |
|---|---|---|
| `TallyDues-Store-Package` (this folder) | `.github/workflows/build-store.yml`, `installer-store/`, `TallyDuesStore/`, `HOW-TO-GET-YOUR-EXE.txt` | `TallyDues-Store-Setup.exe` (needs a signed license per customer) |
| `TallyDues-Store-dev-tools.zip` (separate, private) | `dev-tools/`: `Stamp-For-Customer.ps1`, `LicenseCommon.ps1`, `README.txt` | `.lic` files |
| The Public Beta package (separate) | `.github/workflows/build-public-beta.yml`, `installer/`, `TallyDuesPublicBeta/` | `TallyDues-PublicBeta-Setup.exe` (15-day trial, no license) |

Both installers install to `C:\ProgramData\TallyDues` with no administrator prompt. They have **different installer identities** so each can be updated independently, but they share one install folder, so **do not install both on the same PC**.

## 2. Before the first build

1. **Decide the signing key and back it up.** `LicenseCommon.ps1` holds `$Script:TDV2LicenseKey`. This package contains a freshly generated key. If you have already issued licenses with a different key, paste that key into **both** copies (`dev-tools\` and `TallyDuesStore\Scripts\`) before building; otherwise keep this one. Either way keep the copies byte-identical and keep a private backup of the key: rotating it later invalidates every license already issued. See the *License Generator Guide*, section 8.
2. **Keep `dev-tools` out of the repository.** It creates licenses. It is not part of this folder; keep it on your own PC.
3. Decide whether to buy a code-signing certificate. Without one, Windows SmartScreen may show "Windows protected your PC" for new downloads (users choose **More info > Run anyway**).

## 3. Building a Setup program with GitHub Actions

GitHub Actions provides a Windows machine that has (or installs) Inno Setup. You do not need Inno Setup on your own PC.

### 3.1 One-time: a repository

Create a GitHub repository (a private one is fine). You can use one repository per build, or one for both, as long as the folders do not collide.

### 3.2 Upload the contents

1. Put the **contents** of the Production folder at the top of the repository, including the hidden `.github` folder. The simplest way is git (`git init`, `git add -A`, `git commit`, `git remote add origin <url>`, `git push -u origin main`), which keeps `.github`. In the browser use **Add file > Upload files**; GitHub does **not** unpack `.zip` files, and if the browser skips `.github`, create `.github/workflows/build-store.yml` with **Add file > Create new file** and paste the file's contents in.
2. After the upload this must exist at the top of the repository:

| Build | Required at the top of the repository |
|---|---|
| Public Beta | `.github/workflows/build-public-beta.yml`, `installer/`, `TallyDuesPublicBeta/` |
| Production | `.github/workflows/build-store.yml`, `installer-store/`, `TallyDuesStore/` |

3. Commit the upload.

### 3.3 The build

The workflow starts by itself on every push, and can be started by hand (Actions tab > the workflow > Run workflow). It takes about 2 minutes. It checks out the code, uses Inno Setup (installing it with Chocolatey if the runner does not have it), compiles the `.iss` file, and publishes the Setup program as an artifact.

| Build | Workflow name | Artifact to download | File inside |
|---|---|---|---|
| Public Beta | Build TallyDues public beta installer | `TallyDues-PublicBeta-Setup` | `TallyDues-PublicBeta-Setup.exe` |
| Production | Build TallyDues licensed installer | `TallyDues-Store-Setup` | `TallyDues-Store-Setup.exe` |

When the run shows a green tick, open it and download the artifact (a zip containing the `.exe`).

### 3.4 First-build checks (not yet done anywhere)

If the run fails, open the failed step; the Inno compiler prints the line number. The usual causes are a file missing from the upload (often the hidden `.github` folder, or `installer\*.bmp`), or a folder uploaded one level too deep. Send the log text and the script can be corrected.

## 4. Smoke test before you release (Windows 10/11 64-bit with TallyPrime Release 7+)

1. **Remove any earlier TallyDue/TallyDues** (section 6).
2. Run the new Setup. **Production:** browse to a test `.lic`; try an edited and an expired file first, both must be refused with a reason.
3. Finish Setup; in TallyPrime load the `.tdl` from `C:\ProgramData\TallyDues` (F1 > TDL and Add-On > F4 > Load on startup Yes > browse > Ctrl+A), restart.
4. A welcome notification appears when TallyPrime starts; its text points to the three TallyDues items on the Gateway of Tally menu.
5. Gateway of Tally > **TallyDues Run Check**: notifications appear for real PDCs and invoices.
6. Dashboard and Settings open from the menu; "NO DUES" shows for an empty list; Settings are editable and Ctrl+A saves.
7. **Snooze.** Open the Dashboard before snoozing anything: it must open and show "Nothing snoozed". Then click **Snooze 3 days** on a cheque or invoice notification and reopen the Dashboard: a row appears under SNOOZED REMINDERS with the item count, "Snoozed for 3 days" and the date. `toast.log` shows `snooze board updated`, `C:\ProgramData\TallyDues\snooze.json` exists, and `Get-ScheduledTask TallyDues-Snooze-*` lists the reminder.
8. Start Menu > TallyDues > Alerts > "also show pop-up boxes": pop-ups appear.
9. **Production:** Update License with a new `.lic`; set the PC date past the end date (alert replaced by the expired notice); delete `license.lic` (blocked).
10. Uninstall and confirm `C:\ProgramData\TallyDues` is gone and `Get-ScheduledTask TallyDues-Snooze-*` returns nothing.

The full checklist is in *System Design*, section 15.

## 5. Releasing

- **Public Beta:** give testers `TallyDues-PublicBeta-Setup.exe` and the user guide. Each install starts its own 15-day trial. The trial end date is written to `expiry.txt` at install time; re-running Setup starts another 15 days (acceptable for a "not final" beta).
- **Production:** give each customer `TallyDues-Store-Setup.exe` (the same file for everyone) and their own `.lic` made with `Stamp-For-Customer.ps1`.
- Name each release in your own records (date and installer file size) so you can tell testers which build they have.
- Customers need Windows 10/11 64-bit, TallyPrime Release 7 or later, and no administrator rights.

## 6. Upgrading from the earlier "TallyDue" (before the rename)

The product was renamed from TallyDue to **TallyDues**. The new Setup programs use new installer identities and install to `C:\ProgramData\TallyDues`, so they do **not** replace the old install. On a PC that has the old version:

1. Uninstall the old one: Start Menu > TallyDue > Uninstall TallyDue (or Windows Settings > Apps).
2. In TallyPrime: F1 > TDL and Add-On > F4 and remove the old `TallyDue...tdl` entry.
3. Install the new Setup and load the new `.tdl` from `C:\ProgramData\TallyDues`.

Two add-ons loaded at once would both react to the same events, so remove the old one before loading the new one.

## 7. Maintenance quick reference

| Task | How |
|---|---|
| Change a message | Edit the string in the `.tdl` (ASCII only), re-upload, rebuild. |
| Change graphics | Replace the files in `branding\` and the `.bmp` files under `installer*\` (same names and sizes; the original `.jpg` art is in `installer\art`), rebuild. |
| Issue or renew a license | *License Generator Guide*. |
| Rebuild without changing files | GitHub > Actions > the workflow > Run workflow. |
| Rotate the signing key | Both `LicenseCommon.ps1` copies, rebuild, re-issue every license. |

## 8. Troubleshooting a release

| Symptom | Likely cause and action |
|---|---|
| The workflow does not start | The `.github` folder was not uploaded, or the zip was uploaded instead of its contents. |
| "Workflow file not found" or an empty Actions tab | Same as above: upload the unzipped contents including the hidden folder. |
| Inno reports a missing file | A folder was uploaded one level too deep, or `installer\*.bmp` is missing. |
| SmartScreen warning on the Setup | Expected for an unsigned program: More info > Run anyway; or sign the installer. |
| The customer's Setup refuses the license | Read the reason on the License page; see the *License Generator Guide*, section 6.1. |
| Alerts do not appear after install | Check `toast.log` and `install.log` in `C:\ProgramData\TallyDues`; try the pop-up alert style; see the user guide's troubleshooting table. |
| Snooze button does nothing | See the user guide's troubleshooting table; look for `snooze` lines in `toast.log`. |

*Developed by OpenXpert Solutions.*

# TallyDues - Build and Release Guide

**Audience:** the maintainer at OpenXpert Solutions.

| Build | Where | Result |
|---|---|---|
| Test | GitHub Actions, on every push | Unsigned `TallyDues-Setup.exe` with placeholders. It installs nothing, because it reports "not configured" for any key. |
| Release | Your own PC, `build\Build-Release.ps1` | Signed `TallyDues-Setup.exe`. Your certificate never goes to GitHub. |

## 1. Run the tests

```
powershell -ExecutionPolicy Bypass -File build\Run-Tests.ps1
```

These compile the license helper with the compiler built into Windows and run it against a local mock portal. They do not touch a real portal or `C:\ProgramData\TallyDues`.

To try Setup against a portal on this PC, build the helper with `-LocalPortal`, then compile Setup without `/DRelease`. This allows plain HTTP, only to `127.0.0.1` or `localhost`. Never give that Setup to a customer; `Build-Release.ps1` cannot produce it.

Setup replaces the old beta: if `C:\ProgramData\TallyDue` exists, it asks, removes that folder with its tasks and link handlers, then installs.

```
powershell -ExecutionPolicy Bypass -File build\Build-Helper.ps1 -LocalPortal -PortalUrl http://127.0.0.1:8000 -PublicKey <base64>
```

## 2. The add-on file

Releases ship the plain `TallyDues.tdl` for now, by the owner's decision, because making a `.tcp` needs a licensed TallyPrime Developer: Build is greyed out in the Educational Version. Every customer can read, copy and edit this file. Skip the rest of this section unless you have that license.

To compile it:

1. Open the existing `TallyDues\TallyDues.tpj` in TallyPrime Developer. Never create a new project with this name in that folder: it blanked `TallyDues.tdl`.
2. In **File > Active Project Properties > Build Options**, set **TCP format** to **Inscribed V1**. Inscribed V2 needs TallyPrime Release 7.2, so it would drop customers on 7.0 and 7.1.
3. Leave **Authorisation required** off, so one file works for every customer. The license helper handles licensing.
4. Click **Save and Build**, and put the result at `TallyDues\TallyDues.tcp`.

Setup ships `TallyDues.tcp` when it exists, and `TallyDues.tdl` otherwise. Once a `.tcp` exists, rebuild it after every change to `TallyDues.tdl`, or customers get the old one.

## 3. Build a release

You need:

- Inno Setup 6 installed in its default folder.
- `signtool.exe` from the Windows SDK.
- Your code-signing certificate file.
- The portal's live URL and Ed25519 public key (base64), from the portal team.

```
powershell -ExecutionPolicy Bypass -File build\Build-Release.ps1 -PortalUrl https://your-portal -PublicKey <base64> -CertificatePath C:\path\to\cert.pfx
```

The script asks for the certificate password. It stops if the URL or key is missing or wrong, if the TDL is not ASCII, or if a test fails. It then signs the license helper, Setup and the uninstaller.

The output is `installer\Output\TallyDues-Setup.exe`.

## 4. Before you release

Run the `test-matrix` skill's cases on a clean Windows PC with a licensed TallyPrime, and a real test key from the portal. See `docs\Portal-Team-Requests.md` for what the portal must provide first.

## 5. Maintenance

| Task | How |
|---|---|
| Change a message | Edit the string in `TallyDues.tdl` (ASCII only), recompile the `.tcp` if you ship one, rebuild. |
| Change graphics | Replace the files in `TallyDues\Scripts\branding\` and the `.bmp` files in `installer\` (same names and sizes), rebuild. |
| Portal URL or key changes | Rebuild with the new values. Old installs keep their signed license, but cannot renew until updated. |

## 6. Troubleshooting a build

| Symptom | Likely cause and action |
|---|---|
| Inno reports a missing `TallyDuesLicense.exe` | Run `build\Build-Helper.ps1` first. `Build-Release.ps1` and the workflow do this for you. |
| Customer's Setup says "test build" | They have a build from GitHub Actions, not a release. |
| Alerts do not appear after install | Check `toast.log` and `install.log` in `C:\ProgramData\TallyDues`, and try the pop-up alert style. |

*Developed by OpenXpert Solutions.*

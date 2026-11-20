# TallyDues

TallyPrime add-on by OpenXpert Solutions: Windows notifications and a dashboard for cheques and invoices. TDL calls `wscript.exe`, which runs the VBS and PowerShell scripts. A license key from the subscription portal is required, for trial and paid use.

Stack: TDL, PowerShell 5.1, VBScript, C# 5 (.NET Framework), Inno Setup. `build\Run-Tests.ps1` tests the license helper. Everything else is proven by `/deploy-test`, then a run in TallyPrime.

## Map

- `TallyDues\TallyDues.tdl`: the add-on source, shipped as it is for now. Setup ships a `TallyDues.tcp` instead if one exists.
- `TallyDues\Scripts\`: `TDV2Toast.vbs` (bridge), `notify.ps1` (engine and license gate), `TallyDuesProtocol.*` (toast buttons).
- `TallyDuesLicense\`: `TallyDuesLicense.exe`, the only place with license logic.
- `installer\TallyDues-Setup.iss`: Setup, including the License Key page. `TallyDues\Install-*` and `Uninstall-*` are what it runs.
- `build\`: helper build, tests and signed release. `tests\`: license tests and mock portal.
- `docs\License-Design.md`: the licensing design. `docs\Portal-Team-Requests.md`: what the portal must still provide.
- `C:\ProgramData\TallyDues`: a test install, not source. Uninstalling deletes that whole folder. Change it only through `/deploy-test`, and never run an uninstall script without asking.

## Rules

- TDL must be ASCII. A hook checks each `.tdl` edit.
- TDL fails silently, so "looks right" proves nothing. Only a real run in TallyPrime proves a change works: give the user the exact steps and don't claim success without their result.
- At the end of every chat, update `todo.md` (tasks) and `status.md` (where things stand), and read both when starting.
- `TDV2RunCheck` is frozen: change it only when the user asks. No date logic or expiry test in the TDL. Keep the `TDV2` prefix.
- The final name is `TallyDues` everywhere.
- Ctrl+Y, Ctrl+O and Ctrl+S are out of scope. The Gateway menu items are the only supported route.
- The portal has its own team and repository. Don't change it; add needs to `docs\Portal-Team-Requests.md`.
- Never commit certificates, keys or a real portal public key in place of the placeholders. The live values are passed at build time.
- Customer names and amounts appear in `pending\` and `snooze.json`. Don't read them unless debugging needs it; search for specific lines, and don't quote customer data.
- `TDV2IsReceipt` matches all non-Sales vouchers on purpose. Don't narrow it.
- Search before reading: the Tally samples are 696 files.

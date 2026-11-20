# TallyDues status

Update this file at the end of every chat. The task list is in `todo.md`.

**Last updated:** 2026-11-20

## Where things stand

- **Overall:** not ready to sell. The production plan is **on hold** at the owner's request.
- **Add-on:** the owner reports everything works in TallyPrime on the free Education license, as a `.tdl`. The compiled `.tcp` does not exist yet and is untested.
- **Date logic:** untestable on the Education license, which limits dates. Needs a full license.
- **Code:** branch `license-key` at `9e6f5b4`, not merged. 19 tracked or new files were uncommitted at the audit.
- **Tests:** helper tests pass (229 on 2026-10-31).
- **Setup:** the signed release build has not been run. Inno Setup 6.7.3 and `signtool.exe` are installed. It still needs the live portal URL, live public key, certificate and `TallyDues.tcp`.
- **Portal:** read at commit `f8abc51`. Customer renewal, trials, card payments and renewal emails are missing, so sales would have to be assisted.

## Latest decisions (2026-10-31)

- Background heartbeat: yes.
- Unfreeze `TDV2RunCheck` for production: yes, once it can be tested on a full license.

## Dashboard "Licensed to:" line (2026-11-02)

- The name comes from the portal's signed `organisation` field. The local portal now sends it on `confirm` and `heartbeat`; that change is uncommitted in the portal repo.
- Helper and TDL are changed and uncommitted. The owner confirmed the line works in TallyPrime only with a name typed into `license-status.json`. The full path from portal to Dashboard is not yet tested.
- `TallyDuesLicense.exe` is a local-portal build (`http://127.0.0.1:8000`), not a release build, and is now on the test install. An existing license gets the name only when the key is entered again or renewed to a later date.

## Screen cleanup (2026-11-02)

- Dashboard: "Change the timeframe" has its own line, and the "Alert script in use" line is removed. Settings: blank lines around the fields, and the credit has its own line. Snooze dates now use `d-MMM-yy`.
- Changed in the repo and in `marketing\media.html`, and deployed to the test install. The owner's TallyPrime screenshots confirm the new Dashboard and Settings layouts, and the Dashboard footer is back with a license in place.
- Not yet confirmed: Settings values surviving Ctrl+A and reopen, and a new snooze showing the `d-MMM-yy` date.

## `.tcp` compile (2026-11-02)

- TallyPrime Developer rejected `Word Wrap : Yes` with "Syntax Error: Valid keyword expected". On a Field, the first parameter must be `Always`, `Never` or `Default`; `Yes` is valid only on a Collection. The three Dashboard fields now use `Word Wrap : Always : Low : Yes`, and the two customer name fields add `Max Display Lines : 2`. Tally documents these attributes from Release 7.1. Not yet built or tested.
- Creating a new TallyPrime Developer project named `TallyDues` in the source folder blanked `TallyDues.tdl`, probably because New Project created an empty file over it. It was restored from the test install plus the wrap edits. Use `TallyDues.tpj` from now on, and never create a new project there. The source is still uncommitted, so this could recur.
- No `.tcp` exists. TallyPrime Developer 7.1 here is the Educational Version, with Build (F9) and Analyse greyed out. List Syntax Errors works and reported no errors.
- The owner decided to ship the plain `TallyDues.tdl`, knowing customers can read and copy it. `Build-Release.ps1` and Setup now allow this, and Setup leaves out `*.tpj`.
- Tally's docs say Inscribed V1 works from Tally.ERP 9 Series A Release 5.4. The earlier note that V1 needs Release 7 was wrong. How Word Wrap behaves on releases before 7.1 is unknown.
- The rebuild has not been reported yet. The test install still has the older TDL.

## Release License uninstalls (2026-11-07)

- Release License now asks to release and uninstall. Only after the portal's signed `ok` (nonce checked) and with `license.dat` gone does it ask again and start `unins000.exe`, whose own Yes/No follows. A failed release uninstalls nothing. A new key that arrived during the call stays, and nothing is uninstalled.
- The uninstaller's first question no longer says it will release when no license is left.
- Helper builds, 239 tests pass, Setup compiles. Not deployed to the test install and not yet tried in TallyPrime. The uninstaller's own "Uninstall without releasing?" choice is unchanged.

## "Answer could not be verified" on Change License Key (2026-11-20)

- The installed helper (2026-11-07 build) points at `https://slm.openxpert.cloud` with public key `oui8N2YwBxH81OFF85YqJ9MNOB6o4fLX70ZsVCm0O04=`. No repo, doc or session records where that key came from.
- The helper's requests and signature check match the newest portal code (`origin/main` at `2074823`): the Tally endpoints, `TallyRequest`, `ResponseSigner` and the payloads are unchanged. The live endpoint answers (422 for an empty body).
- Confirmed: the live signing key's public key, printed on the VPS, is `yh1nlWsvtqzDfkDIo4x8mHCXToB0BTLangrcyb0Sui4=`. The installed helper had a different key.
- 239 helper tests pass. `TallyDues\TallyDuesLicense.exe` is rebuilt with `-Release`, `https://slm.openxpert.cloud` and the live key; `Config.cs` keeps its placeholders. The owner copied it to the test install (old helper kept as `TallyDuesLicense.exe.bak-oldkey`). The owner confirmed on 2026-11-20 that Change License Key now accepts a key from the live portal. Release License and the Dashboard with the live portal are not yet tried.
- Committed as `36f027d` on `license-key` and pushed to GitHub (not merged). `installer\Output\TallyDues-Setup.exe` was rebuilt on 2026-11-20 with the live helper. It is **unsigned**, because a signed build needs the certificate in `Build-Release.ps1`. It ships the plain `.tdl` and has not been installed or tested. The two empty `TallyDues.tcp` and `TallyDues.tpj` files at the repo root are left uncommitted.
- Website repo (`yusuf.sabuwala/tallydues`, cloned to `C:\Users\yetah\Projects\tallydues-website`): commit `fa5e31b` on `main`, pushed. It replaces `TallyDues-PublicBeta-Setup.exe` and both `.zip` files with this unsigned Setup, and changes the "15-day beta" wording to say a license key from OpenXpert Solutions is needed. File names and links are unchanged. `npm run build` passes. It is not uploaded to tallydues.app: the README's rsync to Hostinger is still to do. The Public Beta User Guide PDF still describes the old beta.
- Any build or Setup must use this key from now on: `Build-Release.ps1 -PublicKey yh1nl...`.
- The newest portal drops `organisation` from `confirm` and `heartbeat`. The helper accepts that, so the Dashboard "Licensed to:" line will stay empty against the live portal.

## Website release reaching the PC (2026-11-20)

- Problem: a release on the portal website never reached the PC. The helper only called `heartbeat` after a license ended, and the portal's `heartbeat` answers a signed `ok` even for a released machine, because `findMachine` also returns released activations.
- Portal: request item 11 in `docs\Portal-Team-Requests.md` asks for a signed `{"result":"released","nonce":...}` (and `revoked`) answer, plus a warning on the website Release button until then. Not yet sent to the portal team.
- Helper: new `heartbeat` command, started in the background by `notify.ps1` after each licensed gate. It calls the portal at most once a week, or once a day after a failed call, and never blocks alerts. A signed `released` or `revoked` removes the license as Release License does, and Check and the Dashboard footer say why. Unsigned errors change nothing. `renew-check` and the heartbeat no longer re-seal a changed `state.dat`.
- 280 helper tests pass. Uncommitted, not built into `TallyDues\TallyDuesLicense.exe`, not deployed and not tried in TallyPrime. Against the live portal it only brings in early renewals until item 11 ships.

## Next step

Nothing until the owner lifts the hold. Then start with Phase 1 in `todo.md`.

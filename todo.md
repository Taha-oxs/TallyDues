# TallyDues to do

Production plan from the audit on 2026-10-31. **On hold: the owner asked to keep this as a list for now. Start nothing until the owner says so.** Update this file at the end of every chat. See `status.md` for where things stand.

## Decided

- [x] Add a background heartbeat: about once a week, silent, and alerts never stop because the PC is offline. This replaces "no network calls while valid". It brings in renewals, revocations and website releases.
- [x] Unfreeze `TDV2RunCheck` for production, and allow the date logic that real due dates need. It could not be worked on before because the free Education license limits dates, so this work needs a full TallyPrime license to test.

## Waiting on the owner

- [ ] Get a full TallyPrime license for testing date logic.
- [ ] Check the code-signing certificate's expiry and type. Certificates issued since June 2023 need hardware key storage, but `Build-Release.ps1` expects a `.pfx`.
- [ ] Optional: to stop customers reading the source, activate a licensed TallyPrime Developer and compile `TallyDues\TallyDues.tcp` (Inscribed V1) from the existing `TallyDues.tpj`. The owner chose to ship the `.tdl` for now; Build is greyed out in the Educational Version.
- [ ] Decide the minimum TallyPrime version to support. A V1 `.tcp` loads from Tally.ERP 9 Series A Release 5.4, but the Dashboard's Word Wrap and bell notifications need Release 7.0 or 7.1.

## Release blockers on the portal (its team owns these)

- [ ] Customer renewal: `CheckoutService` never sets `subscription_id`, so a repeat purchase creates a new subscription and the key still ends.
- [ ] Self-service trial keys, with no grace days. Today a trial would run for 29 days.
- [ ] Card payments, renewal reminders and scheduled jobs, or launch with assisted sales. Turn on `LICENSE_ISSUE_ON_PAYMENT`, or an admin must generate every key.
- [ ] Sign the machine in `confirm` and `heartbeat` answers (request item 7).
- [ ] `heartbeat` must refuse a released machine with a signed `released` answer (request item 11). Until then a website release does not stop the PC.
- [ ] Admin 2FA setup page, and an offline backup of the signing key.
- [x] Live portal URL and Ed25519 public key: `https://slm.openxpert.cloud`, key `yh1nlWsvtqzDfkDIo4x8mHCXToB0BTLangrcyb0Sui4=` (2026-11-20). Pass both to every build.
- [ ] The newest portal no longer sends `organisation` on `confirm` and `heartbeat`, so the Dashboard "Licensed to:" line stays empty. Ask the portal team to restore it (`docs\Portal-Team-Requests.md`).
- [ ] Commit the portal's `organisation` change: it is uncommitted in the portal's `main` working tree and must reach staging and live.

## Phase 1: Secure the work

- [ ] Commit the uncommitted work on `license-key`, including Release License.
- [ ] Update `docs\Portal-Team-Requests.md` to portal commit `f8abc51`: remove item 5 (6-month plans exist), fix stale item 9, add the blockers above.

## Phase 2: TDL (one change at a time, each tested in TallyPrime)

- [ ] Match custom sales types with `$$IsSales:$VoucherTypeName`. `TDV2IsReceipt` stays as it is.
- [ ] Real due dates and unpaid amounts from bill-wise data (`BillDate`, `BillCreditPeriod`, `ClosingBalance`).
- [ ] Use the real date: `$$MachineDate`, or show both it and Tally's current date.
- [ ] Filter on `TransactionType`, and stop alerting on cleared cheques. Rename the "Customer" column to "Party".
- [ ] Test post-dated cheques with real PDC data: they may be left out, or limited to the open period.
- [ ] Automatic check once per company per day on `Load Company`.
- [ ] Welcome toast on first run only.
- [ ] Tally bell notifications as a backup when Windows blocks toasts (Release 7.0 or later).
- [ ] Group the five menu items under one TallyDues submenu.
- [ ] Enforce 0 to 365 in Settings with `Validate`.
- [ ] Remove `TDV2SalesBills`, the unused count formulas, the Ctrl key blocks, and stale comments. Add `NOT $$IsRemoteCompany` guards and show the version.

## Phase 3: Reliability

- [x] Background heartbeat in the helper (2026-11-20): weekly, started by each licensed alert run, 280 tests pass. Not yet deployed or tried in TallyPrime.
- [ ] Deploy the heartbeat with `/deploy-test` and run test L26 in TallyPrime.
- [ ] Move the alert engine into the signed C# helper, because `wscript` starting hidden PowerShell looks like malware to antivirus.
- [ ] Clear helper message for an unpaid admin-issued key, which returns `subscription_suspended`.
- [ ] Start Menu "Create support file": versions, exit codes and the `tally.ini` TDL line, no customer data.
- [ ] Setup: stop if `{commonappdata}` is not `C:\ProgramData`, add `MinVersion=10.0`, warn on an old TallyPrime, add a EULA and privacy note.
- [ ] Publish a download page and changelog, since there is no update path.

## Phase 4: Release candidate

- [ ] Build and sign Setup, then run the full test matrix on clean Windows 10 and 11 PCs.
- [ ] Test upgrading while TallyPrime is open, and TallyPrime's startup after uninstall.
- [ ] Run end to end against the portal's staging site.

## Phase 5: Limited launch

- [ ] Sell through admin issuance to a few customers: seats 1, discounts for several PCs, NFR keys for partners.
- [ ] Open self-service once the portal's renewal, trial and payment work is done.

## Housekeeping

- [ ] Don't publish the old `TallyDue` folder: its untracked folders hold the retired HMAC key.

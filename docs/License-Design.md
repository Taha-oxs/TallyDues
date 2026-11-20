# TallyDues license design

The portal owns all license decisions. This file records how TallyDues uses it. The portal source is a separate repository, `SubscriptionManagement`.

## Rules set by the owner

- A license key is required for trial and paid use. Trial keys are generated on the portal.
- Internet is needed at install. While a license is valid, TallyDues calls `heartbeat` about once a week in the background (decided 2026-10-31). Being offline never stops alerts before the end date.
- Plans are 6 and 12 months, with no cancellations or refunds. Paid plans have 14 grace days, included in `valid_until`. Trials have none.
- After `valid_until`, alerts stop and a Renew and Cancel notice shows. The Dashboard and Settings stay open.
- No offline activation codes in the first release.
- **Each user has their own key, active on one PC at a time** (changed 2026-10-31; it replaces "activated once, then burned"). All companies on that PC are covered. One Windows account per PC.
- **The customer releases from TallyDues**, not the website. A website release reaches the PC only through the weekly heartbeat, and only once the portal signs a `released` answer (`docs\Portal-Team-Requests.md`, item 11). Until then the PC keeps working. A released key can be activated again, on this PC or another.
- The owner wants released trial keys to stay unusable. TallyDues cannot enforce this: signed answers do not say whether a key is a trial, and the portal lets any released key activate again.
- Renewal keeps the same key on the same PC and only moves the end date. `heartbeat` must keep answering for that PC.
- A PC with no internet cannot release. The customer calls OpenXpert Solutions, which handles it on the portal.
- A seat is taken only after the keyfile is on the PC, and alerts start only after the portal's signed answer is stored.

## Portal API used

All are `POST` under `/api/v1/tally`, sending `machine` (4 hashes), `nonce` and `installer_version`.

| Endpoint | When | Also sends | Signed answer |
|---|---|---|---|
| `validate` | License Key page, Change License Key | `key` | `confirm_token`, `valid_until` |
| `confirm` | After the keyfile is written; this call takes the seat | `confirm_token` | `valid_until`, `organisation` |
| `heartbeat` | About once a week while licensed, and when looking for a renewal | `key` | `valid_until`, `organisation`; or `result` `released` or `revoked` with `nonce` |
| `release` | Release License, uninstall | `key` | `result`, `nonce` |

Errors are unsigned: `{"error": "invalid_key|expired|revoked|subscription_suspended|key_used|limit_reached|rate_limited"}`. `key_used` and `limit_reached` both mean the key is active elsewhere. They can be shown to the user, but never change stored license state.

## Helper exit codes

`TallyDuesLicense.exe` is the only place with license logic. `ExitCodes` in `TallyDuesLicense\Program.cs` is the source; `[Code]` in the `.iss` repeats the ones Setup needs.

| Code | Meaning |
|---|---|
| 0 | OK |
| 10 | License ended |
| 11 | No valid license (missing, edited or forged) |
| 12 | Windows date moved back |
| 13 | License belongs to another machine |
| 20 to 25 | `invalid_key`, `expired`, `revoked`, `subscription_suspended`, key already used, `rate_limited` |
| 30 | Portal unreachable, or server error |
| 31 | Answer failed the signature, nonce or format check |
| 32 | Build holds placeholders |
| 40 | Renewal check found nothing new, or was skipped |
| 41 to 43 | Key change only: same key, too many wrong keys, license set up from another Windows account (43 is also used by release) |
| 44 | Release only: the license file could not be changed |
| 45 | Heartbeat or renewal check: the portal signed `released`, so the license was removed (a signed `revoked` gives 22) |

## How a license is protected

- **Signature:** every answer is checked with Ed25519 against the public key built into the helper. .NET Framework has no Ed25519, so `Ed25519.cs` implements RFC 8032 verification, tested with the RFC vector and signatures from Node.js.
- **Exact bytes:** the signature covers the JSON text of `payload` as PHP wrote it, so the helper cuts that text from the response and does not rebuild it. Laravel escapes `/` in the response, and PHP signed it unescaped, so `\/` is restored.
- **Machine binding:** the portal does not sign the machine, so the nonce is SHA-256 of the sorted machine hashes plus a random value, all stored with the license. Editing the stored hashes breaks the nonce, and copying the file to another PC fails the same 3 of 4 rule the portal uses. A replayed answer has the wrong nonce.
- **Key:** encrypted with Windows DPAPI for the current user in `license.dat`. Never logged or put on a command line; Setup passes it in a temp file.
- **Seal:** `license.dat` and `state.dat` carry an HMAC-SHA-256 seal. The secret is in `seal.key`, protected with DPAPI machine scope because Setup and the alerts can run under different accounts. An edited file, or a missing `seal.key`, stops alerts. HMAC is one-way, so it does not replace the encrypted key, which the renewal check must send.
- **Clock:** `state.dat` holds the latest date seen, with one day of tolerance. The seal stops casual date changes only.

## Activating a key

1. `validate`: takes no seat.
2. Install the files, or in a key change, ask the user to confirm.
3. Write `license.pending`, the sealed keyfile, and read it back. Also check that `license.dat` can be replaced. If not, stop: no seat is taken. The keyfile alone never turns alerts on.
4. `confirm`: the portal takes the seat.
5. Verify the signed answer, save `license.dat` and delete `license.pending`. Alerts start now.

If the connection drops at step 4, the portal may already have taken the seat. The keyfile stays and Retry sends the same token; if the portal refuses that token, the helper gets a new one with `validate`, which accepts the machine that holds the seat. If Setup is cancelled at that point, the install is removed, and running Setup again on this PC with the same key works.

Reinstalling over a licensed PC needs no key, including when the license has ended.

## Releasing a license

`TallyDuesLicense.exe release-window` opens a window from the Gateway menu and the Start Menu, and asks for confirmation with No as the default. `release` does the same with no window, for the uninstaller. Neither has a `tallydues://` route. It also works after the license has ended.

1. Read the stored key. Stop if the file is damaged, belongs to another Windows account or belongs to another PC, because the portal answers `ok` even when no machine matches, which would falsely report success.
2. Check that `license.dat` can be changed, so the seat and the file stay in step.
3. `release`, and check the signature and the nonce.
4. Delete `license.dat` and `license.pending`, clear `state.dat`, update the Dashboard footer and remove the renewal task.

5. Window only: if the step above returned `ok` and `license.dat` is gone, ask (Yes as the default) and start `unins000.exe`, whose own confirmation follows. A failed release uninstalls nothing. A new key that arrived during the call keeps its license, and nothing is uninstalled.

Any failure at step 3 leaves the license working. If the answer is lost, the portal may have freed the seat while this PC stays licensed; the portal's `release` is safe to repeat, so trying again finishes it.

Uninstalling warns first, with No as the default, then releases. If that fails, the user gets Retry, then a warning with No as the default: uninstalling anyway leaves the seat with this PC, and the customer calls OpenXpert Solutions to release it. Reinstalling on the same PC with the same key still works. A silent uninstall tries once and continues.

A release on the website reaches the PC only through the heartbeat below, and only once the portal signs a `released` answer. Until the portal ships that, the PC keeps working until its end date. The website's Release button belongs to the portal and is not changed here.

## Heartbeat

Each licensed alert run starts `TallyDuesLicense.exe heartbeat` in the background. It never delays or blocks the alert.

1. Stop if there is no authentic license, or if `state.dat` was changed. Saving would seal changed records again, so they are left for Check to report.
2. Stop if the last answered call (`LAST_BEAT`) was less than 7 days ago, or the last try (`BEAT_TRY`) less than a day ago. A time in the future counts as old, so a clock set back cannot stop the calls for good.
3. Run the renewal check quietly. A signed, later `valid_until` is saved, as for a renewal.
4. A signed `released` or `revoked` answer with the right nonce removes the license, as Release License does, and records why in `state.dat` (`WITHDRAWN`). Check then explains it, and the Dashboard footer shows it. A new key saved during the call keeps its license. A new license clears the reason.
5. Unsigned errors, a network failure, a bad signature or nonce, or any other `result` change nothing. The next try is the next day.

The portal still answers `ok` for a released machine, so step 4 waits on the portal (`docs\Portal-Team-Requests.md`, item 11).

## Changing the key

Paying after a trial issues a new key; renewals keep the key. `TallyDuesLicense.exe change-key` opens a masked window, reachable from the Gateway menu and the Start Menu, and works after the license has ended. The command takes no key option and has no `tallydues://` route. The TDL only starts the window, so the key never enters Tally, a script, a log or a temp file.

1. Check the format and refuse the key already in use, with no network call.
2. `validate` the new key: nothing is burned and nothing is stored.
3. Show both end dates, warn if the new one is earlier, and ask for confirmation. No is the default.
4. Write the keyfile, `confirm`, verify the signed answer, and save `license.dat` atomically. On failure the old license stays. After a lost connection, Continue retries with the same token and no new `validate`.
5. The old key is removed from this PC and no `release` is sent, so it keeps this PC's seat, which also keeps a replaced trial key from moving. To use a paid key elsewhere, release it before changing keys; the confirmation says so.

A key that cannot be decrypted belongs to another Windows account, so the change is refused. After 5 invalid keys the window waits an hour, which keeps one PC under the portal's limit of 10 an hour per address; this count is unsigned, and the portal's limit is the real one. A renewal check re-reads the license under the lock before saving, so it cannot put an old key back.

## What this does not stop

The scripts and the helper run on the customer's PC and can be patched. The aim is to stop key sharing and casual copying, not a determined cracker. A revoked key keeps working on an offline PC until its end date. A saved copy of an old `license.dat`, restored on the same PC after a key change or a release, also works until its end date, even while the key is active on another PC. .NET cannot reliably wipe a key from memory.

**Open until the portal changes:** signed answers name no machine, so someone with a valid key and a script can make a working `license.dat` for another PC without taking a seat. See item 7 in `Portal-Team-Requests.md`. Copying the file alone does not work. Deleting `state.dat` also clears the latest date seen, since the seal only covers a file that exists. `seal.key` is readable by any program on the PC.

## Files in the install folder

| File | Purpose |
|---|---|
| `TallyDuesLicense.exe` | License helper |
| `license.dat` | Signed answer, machine hashes, random value, encrypted key |
| `license.pending` | Sealed keyfile, waiting for the portal's signed answer |
| `seal.key` | Secret for the HMAC seal, protected for this PC |
| `state.dat` | Latest date seen, warnings shown, last renewal try, last heartbeat and its last try, why the portal withdrew the license, seal |
| `license-status.json` | License text for the Dashboard footer, and the signed organisation for its "Licensed to:" line |
| `license-notice.txt` | Latest message from the gate, for the bridge's pop-up |

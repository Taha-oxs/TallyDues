# TallyDues license design

The portal owns all license decisions. This file records how TallyDues uses it. The portal source is a separate repository, `SubscriptionManagement`.

## Rules set by the owner

- A license key is required for trial and paid use. Trial keys are generated on the portal.
- Internet is needed at install. While a license is valid, TallyDues makes no network calls.
- Plans are 6 and 12 months, with no cancellations or refunds. Paid plans have 14 grace days, included in `valid_until`. Trials have none.
- After `valid_until`, alerts stop and a Renew and Cancel notice shows. The Dashboard and Settings stay open.
- No offline activation codes in the first release.
- **Each user has their own key, and a key is activated once, ever.** It is then burned: no other PC, and no second activation on the same PC, even after uninstalling or reinstalling Windows. All companies on that PC are covered. One Windows account per PC.
- Renewal keeps the same key on the same PC and only moves the end date. `heartbeat` must keep answering for that PC.
- There is no release and no self-service move. A new PC, a dead PC or an uninstall needs a replacement key, issued by a portal admin.
- A key is burned only after the keyfile is on the PC, and alerts start only after the portal's signed answer is stored.

## Portal API used

All are `POST` under `/api/v1/tally`, sending `machine` (4 hashes), `nonce` and `installer_version`.

| Endpoint | When | Also sends | Signed answer |
|---|---|---|---|
| `validate` | License Key page, Change License Key | `key` | `confirm_token`, `valid_until` |
| `confirm` | After the keyfile is written; this call burns the key | `confirm_token` | `valid_until` |
| `heartbeat` | Only when looking for a renewal | `key` | `valid_until` |

Errors are unsigned: `{"error": "invalid_key|expired|revoked|subscription_suspended|key_used|limit_reached|rate_limited"}`. `key_used` and `limit_reached` both mean the key is already burned. They can be shown to the user, but never change stored license state.

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
| 41 to 43 | Key change only: same key, too many wrong keys, license set up from another Windows account |

## How a license is protected

- **Signature:** every answer is checked with Ed25519 against the public key built into the helper. .NET Framework has no Ed25519, so `Ed25519.cs` implements RFC 8032 verification, tested with the RFC vector and signatures from Node.js.
- **Exact bytes:** the signature covers the JSON text of `payload` as PHP wrote it, so the helper cuts that text from the response and does not rebuild it. Laravel escapes `/` in the response, and PHP signed it unescaped, so `\/` is restored.
- **Machine binding:** the portal does not sign the machine, so the nonce is SHA-256 of the sorted machine hashes plus a random value, all stored with the license. Editing the stored hashes breaks the nonce, and copying the file to another PC fails the same 3 of 4 rule the portal uses. A replayed answer has the wrong nonce.
- **Key:** encrypted with Windows DPAPI for the current user in `license.dat`. Never logged or put on a command line; Setup passes it in a temp file.
- **Seal:** `license.dat` and `state.dat` carry an HMAC-SHA-256 seal. The secret is in `seal.key`, protected with DPAPI machine scope because Setup and the alerts can run under different accounts. An edited file, or a missing `seal.key`, stops alerts. HMAC is one-way, so it does not replace the encrypted key, which the renewal check must send.
- **Clock:** `state.dat` holds the latest date seen, with one day of tolerance. The seal stops casual date changes only.

## Activating a key

1. `validate`: burns nothing.
2. Install the files, or in a key change, ask the user to confirm.
3. Write `license.pending`, the sealed keyfile, and read it back. Also check that `license.dat` can be replaced. If not, stop: nothing is burned. The keyfile alone never turns alerts on.
4. `confirm`: the portal burns the key.
5. Verify the signed answer, save `license.dat` and delete `license.pending`. Alerts start now.

If the connection drops at step 4, the portal may already have burned the key. The keyfile stays and Retry sends the same token, so **`confirm` must be safe to repeat** (item 10 in `Portal-Team-Requests.md`). Until then a lost answer costs the customer a replacement key. If Setup is cancelled at that point, the install is removed and the key may be burned; running Setup again says so.

Reinstalling over a licensed PC needs no key, including when the license has ended, because the old key can never be entered again. Uninstalling warns first, with No as the default, then deletes the license.

## Changing the key

Paying after a trial issues a new key; renewals keep the key. `TallyDuesLicense.exe change-key` opens a masked window, reachable from the Gateway menu and the Start Menu, and works after the license has ended. The command takes no key option and has no `tallydues://` route. The TDL only starts the window, so the key never enters Tally, a script, a log or a temp file.

1. Check the format and refuse the key already in use, with no network call.
2. `validate` the new key: nothing is burned and nothing is stored.
3. Show both end dates, warn if the new one is earlier, and ask for confirmation. No is the default.
4. Write the keyfile, `confirm`, verify the signed answer, and save `license.dat` atomically. On failure the old license stays. After a lost connection, Continue retries with the same token and no new `validate`.
5. The old key is removed from this PC. It stays burned on the portal, and no `release` is sent.

A key that cannot be decrypted belongs to another Windows account, so the change is refused. After 5 invalid keys the window waits an hour, which keeps one PC under the portal's limit of 10 an hour per address; this count is unsigned, and the portal's limit is the real one. A renewal check re-reads the license under the lock before saving, so it cannot put an old key back.

## What this does not stop

The scripts and the helper run on the customer's PC and can be patched. The aim is to stop key sharing and casual copying, not a determined cracker. A revoked key keeps working on an offline PC until its end date. A saved copy of an old `license.dat`, restored on the same PC after a key change, also works until its end date. .NET cannot reliably wipe a key from memory.

**Open until the portal changes:** signed answers name no machine, so someone with a valid key and a script can make a working `license.dat` for another PC without taking a seat. See item 7 in `Portal-Team-Requests.md`. Copying the file alone does not work. Deleting `state.dat` also clears the latest date seen, since the seal only covers a file that exists. `seal.key` is readable by any program on the PC.

## Files in the install folder

| File | Purpose |
|---|---|
| `TallyDuesLicense.exe` | License helper |
| `license.dat` | Signed answer, machine hashes, random value, encrypted key |
| `license.pending` | Sealed keyfile, waiting for the portal's signed answer |
| `seal.key` | Secret for the HMAC seal, protected for this PC |
| `state.dat` | Latest date seen, warnings shown, last renewal try, seal |
| `license-status.json` | License text for the Dashboard footer |
| `license-notice.txt` | Latest message from the gate, for the bridge's pop-up |

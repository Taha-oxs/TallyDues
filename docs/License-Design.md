# TallyDues license design

The portal owns all license decisions. This file records how TallyDues uses it. The portal source is a separate repository, `SubscriptionManagement`.

## Rules set by the owner

- A license key is required for trial and paid use. Trial keys are generated on the portal.
- Internet is needed at install. While a license is valid, TallyDues makes no network calls.
- Plans are 6 and 12 months, with no cancellations or refunds. Paid plans have 14 grace days, included in `valid_until`. Trials have none.
- After `valid_until`, alerts stop and a Renew and Cancel notice shows. The Dashboard and Settings stay open.
- No offline activation codes in the first release.
- One seat per machine. All companies on that machine are covered. One Windows account per PC.

## Portal API used

All are `POST` under `/api/v1/tally`, sending `machine` (4 hashes), `nonce` and `installer_version`.

| Endpoint | When | Also sends | Signed answer |
|---|---|---|---|
| `validate` | License Key page | `key` | `confirm_token`, `seats_free`, `valid_until` |
| `confirm` | After files are installed | `confirm_token` | `valid_until` |
| `heartbeat` | Only when looking for a renewal | `key` | `valid_until` |
| `release` | Uninstall | `key` | `result` |

Errors are unsigned: `{"error": "invalid_key|expired|revoked|subscription_suspended|limit_reached|rate_limited"}`. They can be shown to the user, but never change stored license state.

## Helper exit codes

`TallyDuesLicense.exe` is the only place with license logic. `ExitCodes` in `TallyDuesLicense\Program.cs` is the source; `[Code]` in the `.iss` repeats the ones Setup needs.

| Code | Meaning |
|---|---|
| 0 | OK |
| 10 | License ended |
| 11 | No valid license (missing, edited or forged) |
| 12 | Windows date moved back |
| 13 | License belongs to another machine |
| 20 to 25 | `invalid_key`, `expired`, `revoked`, `subscription_suspended`, `limit_reached`, `rate_limited` |
| 30 | Portal unreachable, or server error |
| 31 | Answer failed the signature, nonce or format check |
| 32 | Build holds placeholders |
| 40 | Renewal check found nothing new, or was skipped |

## How a license is protected

- **Signature:** every answer is checked with Ed25519 against the public key built into the helper. .NET Framework has no Ed25519, so `Ed25519.cs` implements RFC 8032 verification, tested with the RFC vector and signatures from Node.js.
- **Exact bytes:** the signature covers the JSON text of `payload` as PHP wrote it, so the helper cuts that text from the response and does not rebuild it. Laravel escapes `/` in the response, and PHP signed it unescaped, so `\/` is restored.
- **Machine binding:** the portal does not sign the machine, so the nonce is SHA-256 of the sorted machine hashes plus a random value, all stored with the license. Editing the stored hashes breaks the nonce, and copying the file to another PC fails the same 3 of 4 rule the portal uses. A replayed answer has the wrong nonce.
- **Key:** encrypted with Windows DPAPI for the current user in `license.dat`. Never logged or put on a command line; Setup passes it in a temp file.
- **Clock:** `state.dat` holds the latest date seen, with one day of tolerance. It is unsigned, so it stops casual date changes only.

## What this does not stop

The scripts and the helper run on the customer's PC and can be patched. The aim is to stop key sharing and casual copying, not a determined cracker. A revoked key keeps working on an offline PC until its end date.

## Files in the install folder

| File | Purpose |
|---|---|
| `TallyDuesLicense.exe` | License helper |
| `license.dat` | Signed answer, machine hashes, random value, encrypted key |
| `state.dat` | Latest date seen, warnings shown, last renewal try |
| `license-status.json` | License text for the Dashboard footer |
| `license-notice.txt` | Latest message from the gate, for the bridge's pop-up |

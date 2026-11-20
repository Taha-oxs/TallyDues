# Requests for the portal team

TallyDues needs these from the portal before release. File references are to the `SubscriptionManagement` repository at commit `60ab30e`.

## Required

1. **Live URL and Ed25519 public key.** The public key is the base64 of the 32-byte key. `Build-Release.ps1` needs both.
2. **Self-service trial keys.** A signed-up user generates a 15-day key: one seat, one trial per machine and per company. Today `trial_days` exists on `Plan` but nothing uses it, no code starts a subscription as `trialing`, and keys are issued only for paid orders.
3. **No grace for trials.** `ActivationService::validUntil` adds `grace_days` (14) to every license, so a 15-day trial would run for 29 days.
4. **New key from trial to paid, same key on renewal.** Decided by the owner on 2026-10-31. Paying after a trial issues a new key, which the customer enters in TallyDues Change License Key. Renewals must keep the key, because TallyDues finds the new end date with `heartbeat`. Please also confirm that the paid key can activate on a machine that used the trial.
5. **6-month plan.** Plans support only `month` and `year`, and `OrderActivationService::periodEnd` (line 112) gives one month for anything but `year`.
6. **Renewal link.** The Renew button opens `/portal`. Please confirm that a customer can renew from there, or give us a direct address. Please also confirm that checkout creates a renewal order linked to the existing subscription, so that the key does not change; we did not verify this.

7. **Name the machine in signed answers (security).** Add `action`, `machine_components` and `key_hash` to the signed `confirm` and `heartbeat` answers. Today the payload names no machine and the nonce is chosen by the client. Someone with one valid key and a small script can call `heartbeat` using an activated PC's hashes and a nonce made for another PC, then write the answer into `license.dat` there. It passes on any number of PCs and takes no seat. TallyDues cannot close this alone. It will check these fields once the portal sends them. `key_hash` is peppered, so the client cannot compare it: please also sign a plain SHA-256 of the canonical key, so that a changed key can be tied to its signed answer.
8. **Reject a missing period.** `validUntil` uses `now()` when `current_period_end` is null, which gives a rolling end date 14 days ahead on every call.
9. **One activation ever (decided by the owner on 2026-10-31).** Each user has their own key, used once and then burned. The code at `60ab30e` still works by seats, so please confirm whether a newer version exists. It needs:
   - `max_activations` of 1, whatever `Subscription.seats` says, so a company with 5 PCs gets 5 keys. Please tell us how a purchase of several keys will work.
   - `validate` and `confirm` refuse a key already activated, including on the same PC. `ActivationService::validate` and `claimSeat` let the same machine through today, and `release` frees a seat that can be taken again.
   - The answer is `key_used`, or the existing `limit_reached`. TallyDues treats both the same.
   - `heartbeat` keeps answering for the activated PC, because renewals keep the key. It must still match by 3 of 4 hashes.
   - TallyDues no longer calls `release`. Please remove it from My licenses, and remove the owner's Release button.
   - An admin can issue a replacement key for a new or dead PC, and the customer can see it under My licenses. The old key stays burned.
10. **Make `confirm` safe to repeat.** `Cache::pull` deletes the token before the work, so if the answer is lost the retry gets `invalid_key`, and a new `validate` then refuses the burned key. A customer would lose a key to a dropped connection. Please keep the token until it expires, and let the same token from the same machine answer again with a new signature for the new nonce, without another activation. Please burn the key only in this call.

11. **Tell a PC that its machine was released (added 2026-11-20).** A release from the website (`CustomerLicenseController::releaseMachine`) never reaches the PC, so it keeps working while the key is activated on another PC. TallyDues now calls `heartbeat` about once a week, but the portal still answers `ok` for a released machine: in `ActivationService::heartbeat` (`origin/main` at `2074823`), `findMachine` also returns activations whose `status` is `released`, and only a null match throws `revoked`. It also updates `last_seen_at` and signs a `valid_until`, so a released PC could even renew. Please:
    - In `heartbeat`, treat a match whose `status` is not `active` like no match.
    - For that case, answer HTTP 200 with a signed payload `{"result":"released","nonce":"<the request's nonce>"}`, in the usual `{"payload": {...}, "signature": "..."}` shape. TallyDues ignores unsigned errors by design, because anyone on the network path could send one. A signed answer bound to the nonce is the only answer that will remove the license from the PC.
    - For an admin-revoked license, answer the same way with `"result":"revoked"`. TallyDues handles both.
    - Keep answering `ok` as today for an active machine, matched by 3 of 4 hashes.
    - Until this ships, a website release does not stop the PC. Please show a warning on the Release button saying that the PC keeps working until its end date and that customers should release from TallyDues instead.

## Optional

12. Return the paid period's end date as well. TallyDues warns relative to `valid_until`, which includes the 14 grace days, so "14 days before the end" means the day the paid period ends.

## Behaviour TallyDues relies on

- Routes stay under `/api/v1/tally`, and answers keep the shape `{"payload": {...}, "signature": "..."}`.
- `payload` is signed as `json_encode` with `JSON_UNESCAPED_SLASHES`, and values contain no non-ASCII text.
- `confirm_token` stays 48 characters, and machine hashes stay 64 hex characters.
- `heartbeat` answers normally once a lapsed subscription is renewed.
- A burned key stays burned after renewal and after a replacement key is issued.

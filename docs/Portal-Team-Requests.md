# Requests for the portal team

TallyDues needs these from the portal before release. File references are to the `SubscriptionManagement` repository at commit `60ab30e`.

## Required

1. **Live URL and Ed25519 public key.** The public key is the base64 of the 32-byte key. `Build-Release.ps1` needs both.
2. **Self-service trial keys.** A signed-up user generates a 15-day key: one seat, one trial per machine and per company. Today `trial_days` exists on `Plan` but nothing uses it, no code starts a subscription as `trialing`, and keys are issued only for paid orders.
3. **No grace for trials.** `ActivationService::validUntil` adds `grace_days` (14) to every license, so a 15-day trial would run for 29 days.
4. **Trial to paid keeps the same key.** TallyDues finds the new end date with `heartbeat`, so a new key would force a reinstall.
5. **6-month plan.** Plans support only `month` and `year`, and `OrderActivationService::periodEnd` (line 112) gives one month for anything but `year`.
6. **Renewal link.** The Renew button opens `/portal`. Please confirm that a customer can renew from there, or give us a direct address. Please also confirm that checkout creates a renewal order linked to the existing subscription, so that the key does not change; we did not verify this.

## Optional

7. Include `key_hash` and `machine_hash` in the signed `validate`, `confirm` and `heartbeat` answers, as `activateOffline` already does. TallyDues binds to the machine through the nonce today, so this would be extra protection.
8. Return the paid period's end date as well. TallyDues warns relative to `valid_until`, which includes the 14 grace days, so "14 days before the end" means the day the paid period ends.

## Behaviour TallyDues relies on

- Routes stay under `/api/v1/tally`, and answers keep the shape `{"payload": {...}, "signature": "..."}`.
- `payload` is signed as `json_encode` with `JSON_UNESCAPED_SLASHES`, and values contain no non-ASCII text.
- `confirm_token` stays 48 characters, and machine hashes stay 64 hex characters.
- `heartbeat` answers normally once a lapsed subscription is renewed.

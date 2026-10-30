---
name: license-security
description: Design and review the TallyDue side of licensing, including key entry, portal API calls, signed responses, machine identity, heartbeat, offline activation, grace periods and tamper resistance. Use before writing or changing any license, activation, trial or expiry code.
---

# License and API security

The design is decided in `docs/Subscription & License Management Portal Architecture.md`. Read its "Tally Dues license keys and activation" section first, and follow it. If code needs something different, raise it rather than quietly changing the design.

Be honest about the limit: the client can be patched. The design accepts this risk, aiming to stop key sharing and casual copying, not a determined cracker.

## Client responsibilities

- **Key:** 32 hex characters in eight groups of four. Never log it or put it on a command line.
- **Install:** call `POST /v1/tally/validate` (changes nothing) before installing and `POST /v1/tally/confirm` after success. A failed or cancelled install must not take a seat.
- **Uninstall:** call `POST /v1/tally/release`, or show a release code when offline.
- **Heartbeat:** call `POST /v1/tally/heartbeat` on start and about every 7 days. Store the signed `valid_until`, and stop with a renewal message once it passes.
- **Offline:** installation code, then a signed activation code bound to the key, machine hash and nonce, plus refresh and release codes.
- **Signatures:** verify every response with the built-in Ed25519 public key and check the echoed nonce. Reject anything unsigned, or signed for another machine or nonce.
- **Machine identity:** MachineGuid, CPU ID, motherboard serial and system disk serial, each salted and hashed on the machine. Send only hashes.
- **Clock:** store the latest date seen, and refuse to run if the clock moves back.
- **Errors:** handle `invalid_key`, `expired`, `revoked`, `subscription_suspended`, `limit_reached` and `rate_limited`, each with its own customer message.

A short outage or days without internet must never stop a customer inside the grace period. A license failure must never crash or block TallyPrime itself.

## Open design gaps

Raise these before building the affected part:

- **No app process:** the design assumes an "installed app" runs the heartbeat, but TallyDue is TDL plus scripts that run on demand. Decide what runs it and what stores `valid_until`.
- **Ed25519 on Windows:** Windows PowerShell 5.1, .NET Framework and Inno Setup's Pascal script have no built-in Ed25519. A small signed helper or bundled library is needed; test it on a clean PC. The doc lists this as an open question.
- **Stored state:** the doc says "no license file", but `valid_until`, the activation code and the latest date seen must be stored somewhere, and a user can edit them. Signed values resist editing, but the latest date seen is not signed.
- **Enforcement:** decide how the TDL Dashboard and Settings learn the license state, since TDL cannot verify signatures itself.
- **Stop or read-only:** the doc leaves open what happens after `valid_until`.

## Current beta gaps

The doc issues keys only after a verified payment, so the 15-day trial goes away for paid builds. Until then:

- The trial is enforced only in `TDV2Toast.vbs`, using `expiry.txt`, which anyone can edit.
- Rerunning the installer starts a new trial.
- Setting the Windows clock back extends the trial.
- The Dashboard and Settings do not check expiry.

## Rules

- Server-side secrets (pepper, Ed25519 private key, bank keys) never enter this repo. Only the public key ships.
- Keep the portal URL in one place, use HTTPS only, and keep certificate validation on.
- Treat every server field as untrusted until its signature passes.
- Check the installer pins nothing that would break when the portal certificate renews.

## Test before release

Edited stored state, activation code moved to another PC, clock moved back, portal down, expired subscription, revoked key, `limit_reached`, replayed response, reinstall on the same machine (no new seat), disk replaced (still 3 of 4), cancelled install (no seat taken).

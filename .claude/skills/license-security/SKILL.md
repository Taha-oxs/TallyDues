---
name: license-security
description: Design and review the TallyDues side of licensing, including key entry, portal API calls, signed responses, machine identity, renewal checks and tamper resistance. Use before writing or changing any license, activation, trial or expiry code.
---

# License and API security

The design is in `docs/License-Design.md`. Read it first and follow it. If code needs something different, raise it rather than quietly changing the design. Needs from the portal go in `docs/Portal-Team-Requests.md`; its team owns it.

Be honest about the limit: the client can be patched. The design accepts this risk, aiming to stop key sharing and casual copying, not a determined cracker.

## Fixed decisions

- **Internet:** needed at install. While licensed, each alert run starts a background `heartbeat` that calls the portal about once a week (owner's decision, 2026-10-31). It brings in early renewals and a signed `released` or `revoked`. Being offline never stops alerts before the end date.
- **One place:** all license logic is in `TallyDuesLicense.exe`. Setup and `notify.ps1` use its exit codes. The TDL has no date logic and only displays `license-status.json`.
- **One PC at a time:** each user has their own key. Customers release it from TallyDues or by uninstalling, then activate it again on this PC or another. Renewal keeps the key. A PC with no internet needs a call to OpenXpert Solutions.
- **Trial:** a portal key like any other. There is no trial code in the client.
- **No offline activation** in the first release.

## Rules

- **Key:** 32 hex characters in eight groups of four. Never log it or put it on a command line. Setup passes it in a temp file, and it is stored with DPAPI.
- **Key change:** the key is typed into the helper's own window only. Never add a key option to `change-key`, a `tallydues://` route, or key handling in the TDL. The old license stays until the new one is saved.
- **Install:** `validate` changes nothing. The sealed keyfile `license.pending` is written first, then `confirm` takes the seat, and alerts start only once the signed answer is stored. A failed install must not take a seat.
- **Seal:** `license.dat` and `state.dat` carry an HMAC seal with a DPAPI machine-scope secret. It stops casual edits only. Never replace the encrypted key with an HMAC: the renewal check must send the key.
- **Signatures:** verify every answer with the built-in Ed25519 public key and check the nonce. Verify the raw text of `payload`; never parse and rebuild it.
- **Errors:** unsigned, so they may be shown but never change stored state. Only a signed answer with the right nonce may remove a license (`released`, `revoked`).
- **Tampered state:** never save `state.dat` when its seal fails, or the change would be sealed again.
- **Machine:** only hashes leave the PC. The nonce binds a stored answer to the machine, using the portal's 3 of 4 rule.
- **Failure:** a license failure must never crash or block TallyPrime. If the check cannot run, alerts stop with a message (fail closed).
- **Secrets:** the Ed25519 private key and portal secrets never enter this repo. Only the public key ships, passed at build time.
- **Portal URL:** in one place, HTTPS only in a real build, certificate validation on, no pinning that would break when the certificate renews, no redirects followed. Renew never takes an address from a link.
- **Crypto changes:** `Ed25519.cs` must keep passing the RFC vector and the Node.js vectors in `tests/`.

## Test before release

`build\Run-Tests.ps1` covers the helper. On a real PC with a real test key: portal down, a key active on another PC refused, release online and offline, reinstall over a licensed PC with the box empty, uninstall warning, disk replaced (still 3 of 4), cancelled install (no seat taken), answer lost during `confirm`, standard user, and whether a PC's board or disk serial reads correctly.

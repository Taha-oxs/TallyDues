---
name: license-security
description: Design and review the TallyDues side of licensing, including key entry, portal API calls, signed responses, machine identity, renewal checks and tamper resistance. Use before writing or changing any license, activation, trial or expiry code.
---

# License and API security

The design is in `docs/License-Design.md`. Read it first and follow it. If code needs something different, raise it rather than quietly changing the design. Needs from the portal go in `docs/Portal-Team-Requests.md`; its team owns it.

Be honest about the limit: the client can be patched. The design accepts this risk, aiming to stop key sharing and casual copying, not a determined cracker.

## Fixed decisions

- **Internet:** needed only at install and while looking for a renewal. A valid license makes no network calls, so there is no periodic heartbeat. `heartbeat` runs only after the user clicks Renew or once the license has ended.
- **One place:** all license logic is in `TallyDuesLicense.exe`. Setup and `notify.ps1` use its exit codes. The TDL has no date logic and only displays `license-status.json`.
- **Trial:** a portal key like any other. There is no trial code in the client.
- **No offline activation** in the first release.

## Rules

- **Key:** 32 hex characters in eight groups of four. Never log it or put it on a command line. Setup passes it in a temp file, and it is stored with DPAPI.
- **Install:** `validate` changes nothing, and `confirm` takes the seat after files are in place. A failed or cancelled install must not take a seat.
- **Signatures:** verify every answer with the built-in Ed25519 public key and check the nonce. Verify the raw text of `payload`; never parse and rebuild it.
- **Errors:** unsigned, so they may be shown but never change stored state.
- **Machine:** only hashes leave the PC. The nonce binds a stored answer to the machine, using the portal's 3 of 4 rule.
- **Failure:** a license failure must never crash or block TallyPrime. If the check cannot run, alerts stop with a message (fail closed).
- **Secrets:** the Ed25519 private key and portal secrets never enter this repo. Only the public key ships, passed at build time.
- **Portal URL:** in one place, HTTPS only in a real build, certificate validation on, no pinning that would break when the certificate renews, no redirects followed. Renew never takes an address from a link.
- **Crypto changes:** `Ed25519.cs` must keep passing the RFC vector and the Node.js vectors in `tests/`.

## Test before release

`build\Run-Tests.ps1` covers the helper. On a real PC with a real test key: portal down, `limit_reached`, reinstall on the same machine (no new seat), disk replaced (still 3 of 4), cancelled install (no seat taken), standard user, and whether a PC's board or disk serial reads correctly.

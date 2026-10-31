---
name: release
description: Run the TallyDues release checklist and give a ship or do-not-ship decision.
disable-model-invocation: true
---

# Release checklist

Stop and report at the first blocker. Never publish or upload anything without the user's explicit go-ahead.

1. Confirm the git tree is clean, and record the commit and version number.
2. Confirm that `docs\Portal-Team-Requests.md` has no required item still open.
3. Run `build\Run-Tests.ps1`.
4. Run the `release-security-review` skill. Any confirmed high-severity issue blocks the release.
5. Check the `test-matrix` run log. Every case must have a pass that is still valid, on both installs.
6. Run the `docs-sync` skill and fix any drift.
7. Confirm `TallyDues\TallyDues.tcp` was compiled from the current `TallyDues.tdl`.
8. The user runs `build\Build-Release.ps1` on their PC, since it needs the certificate. Confirm Setup, the uninstaller and `TallyDuesLicense.exe` are signed, and list the package contents.
9. Test the final Setup.exe, not the source files: fresh install and reinstall on a clean PC, with a real test key.
10. Report: version, commit, SHA-256 of Setup.exe, test status, known issues, and the ship or do-not-ship decision.

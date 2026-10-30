---
name: release
description: Run the TallyDue release checklist and give a ship or do-not-ship decision.
disable-model-invocation: true
---

# Release checklist

Stop and report at the first blocker. Never publish or upload anything without the user's explicit go-ahead.

1. Confirm the git tree is clean, and record the commit and version number.
2. Confirm which TDL and scripts ship, and that names are consistent across the installer, TDL, link handler, uninstaller and docs.
3. Run the `release-security-review` skill. Any confirmed high-severity issue blocks the release.
4. Check the `test-matrix` run log. Every case must have a pass that is still valid, on both installs.
5. Run the `docs-sync` skill and fix any drift.
6. Follow the `windows-installer` skill to build Setup. Confirm Setup and its uninstaller are signed, and list the package contents.
7. Test the final Setup.exe, not the source files: fresh install and upgrade on a clean PC.
8. Report: version, commit, SHA-256 of Setup.exe, test status, known issues, and the ship or do-not-ship decision.

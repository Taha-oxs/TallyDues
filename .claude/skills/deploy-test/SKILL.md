---
name: deploy-test
description: Copy changed files from this repo to the test install at C:\ProgramData\TallyDues so they can be tested in TallyPrime.
disable-model-invocation: true
---

# Deploy to the test install

1. Read `TDL=` in `C:\Program Files\TallyPrime\tally.ini` to confirm which add-on is loaded.
2. Compare the repo with the install folder, and list what differs. Include only program files: `.tdl`, `.tcp`, `.ps1`, `.vbs`, `TallyDuesLicense.exe` and `branding\`.
3. Show the list and wait for the user's go-ahead if any installed file looks newer or has changes the repo lacks. Bring those into the repo first, or they would be lost.
4. Copy the files. Never touch `pending\`, `snooze.json`, `license.dat`, `state.dat`, `license-status.json`, `license-notice.txt`, `notify-mode.txt`, `last-toast.txt`, logs or `unins000.*`.
5. A helper built without a real portal URL and key cannot activate anything. Say which build was copied.
6. Tell the user whether TallyPrime needs restarting (it does for any add-on change), and give the shortest list of cases from the `test-matrix` skill.

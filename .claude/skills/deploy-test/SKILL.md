---
name: deploy-test
description: Copy changed files from this repo to the test install at C:\ProgramData\TallyDue so they can be tested in TallyPrime.
disable-model-invocation: true
---

# Deploy to the test install

1. Read `TDL=` in `C:\Program Files\TallyPrime\tally.ini` to confirm which TDL is loaded.
2. Compare the repo with the install folder, and list what differs. Include only program files: `.tdl`, `.ps1`, `.vbs` and `branding\`.
3. Show the list and wait for the user's go-ahead if any installed file looks newer or has changes the repo lacks. Bring those into the repo first, or they would be lost.
4. Copy the files. Never touch `pending\`, `snooze.json`, `expiry.txt`, `notify-mode.txt`, `last-toast.txt`, logs or `unins000.*`.
5. A TDL copied from the repo keeps the values stamped in it, including the script path, timeframes and expiry. Check that `TDV2Script` is still valid for this PC and report the expiry date.
6. Tell the user whether TallyPrime needs restarting (it does for any TDL change), and give the shortest list of cases from the `test-matrix` skill.

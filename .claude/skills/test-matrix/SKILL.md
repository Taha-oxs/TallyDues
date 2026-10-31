---
name: test-matrix
description: Fixed TallyDues test checklist for the two TallyPrime installs on the Education license. Use when planning or recording tests, after any TDL, script or installer change, or before a release, to work out which cases must be re-run and which are still valid.
---

# Test matrix

Give the user the shortest list of cases that covers the change, then record their results. Never mark a case as passed without a result from a real run.

## Environments

| ID | Path | Version | License |
|---|---|---|---|
| A | `C:\Program Files\TallyPrime` | 1.1.7.1 | Education |
| B | `C:\Program Files\TallyPrimeDeveloper` | 1.1.7.1 | Education |

**Education mode:** it is known for restricting voucher dates to certain days of the month (commonly 1, 2 and 31). Confirm this on your release, as it limits which due dates can be tested. Use the current date in Tally to place them inside the alert window, and note that a licensed copy still needs a pass before a paid release.

## What to re-run

| Change | Run |
|---|---|
| TDL menus | S |
| TDL collections or dates | D, A |
| `notify.ps1`, `TDV2Toast.vbs` | A, N |
| Link handlers | N |
| Installer | I, then S |
| License helper or gate | `build\Run-Tests.ps1`, L, then S |
| Anything, before release | Everything, on A and B |

## Cases

**S: Smoke** (always)
- S1 Add-on loads with no error, and the Gateway shows four items
- S2 Welcome notification on start
- S3 Dashboard shows the expected script path and license text

**D: Dates and windows**
- D1 Nothing in window shows "Nothing to report"
- D2 On the first day and on the last day of the window, included
- D3 One day outside on either side, excluded
- D4 Timeframes of 0 and 365
- D5 Settings saved with Ctrl+A survive a restart

**A: Alerts and data**
- A1 PDC only, A2 invoice only, A3 both
- A4 Exactly 5 and then 6 items, showing "(+1 more)"
- A5 Cancelled and optional vouchers excluded
- A6 Non-Sales voucher types behave as intended
- A7 Party names with `&`, `"`, `'`, `%`, and a long name
- A8 Two companies open; company switch
- A9 Currency symbol appears correctly, and Run Check still runs for a company with no symbol

**N: Notifications**
- N1 Toast shown, N2 pop-up mode, N3 Focus Assist on
- N4 Snooze 1 day and 3 days: task created, Dashboard list updated, no customer text in the task's arguments
- N5 Snoozed reminder fires, including after a restart
- N6 View Details brings TallyPrime forward
- N7 Old toast whose pending file is gone gives no crash
- N8 Repeated Run Check within 20 seconds shows nothing new, for cheques and invoices alike

**I: Installer**
- I1 Fresh, I2 reinstall with key box empty, I3 uninstall leaves no task or link handler
- I4 Standard user
- I5 Cancel at each page: nothing installed, key not burned
- I6 Uninstall warning: No is the default and keeps everything; Yes removes everything

**L: License** (needs the portal, or a build against a mock)
- L1 Valid paid key, L2 trial key, L3 wrong format, L4 each portal error shows its own message
- L5 No internet at License Key page: message, Retry works once online
- L6 Internet lost before `confirm`: Retry works; Cancel removes the install and does not burn the key
- L6a Answer lost after the portal burned the key (needs the portal's repeatable `confirm`): Retry finishes, one activation on the portal
- L7 Portal returns a wifi login page: not trusted, nothing installed
- L8 Clock moved back: alerts paused with a message
- L9 Edited `license.dat`, and `license.dat` copied to another PC: refused
- L10 Warnings at 14, 7 and 1 days, once each
- L11 After the end date: alerts stop, Renew and Cancel shown, Dashboard and Settings open, TallyPrime usable
- L12 Renew offline: browser fails to load; connect; after renewal on the portal, alerts return with no reinstall
- L13 Pop-up mode: Yes on the pop-up does what Renew does
- L14 A key already used, on this PC or another: refused with the replacement key message
- L15 Setup over a licensed PC, valid and ended, with the box empty: installs, license kept
- L16 Change License Key opens from the Gateway and the Start Menu, in front of TallyPrime, with the key masked and copy refused
- L17 Change to a good key: both dates shown, No changes nothing, Yes activates it, the Dashboard footer updates
- L18 Change with a wrong key, the same key, no internet and a used key: own message each, old license keeps working
- L19 Change after the license has ended: alerts return. Five wrong keys: an hour's wait
- L21 Edited `state.dat`, or deleted `seal.key`: alerts stop with a message
- L20 Clipboard no longer holds the key after the window closes; text copied earlier that is not the key stays

## Run log

Save results in `.claude/skills/test-matrix/runs.md`, one row each: date, build or file changed, environment, case, pass or fail, and notes. A pass stays valid until a change in the table above affects it, so skip cases that are still valid and say so.

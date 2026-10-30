---
name: test-matrix
description: Fixed TallyDue test checklist for the two TallyPrime installs on the Education license. Use when planning or recording tests, after any TDL, script or installer change, or before a release, to work out which cases must be re-run and which are still valid.
---

# Test matrix

Give the user the shortest list of cases that covers the change, then record their results. Never mark a case as passed without a result from a real run.

## Environments

Fill in the two installs the first time this skill is used. Only one `tally.exe` install was found on this PC (plus TallyPrime Developer).

| ID | Path | Version | License |
|---|---|---|---|
| A | `C:\Program Files\TallyPrime` | 1.1.7.1 | Education |
| B | Ask the user | Ask the user | Education |

**Education mode:** it is known for restricting voucher dates to certain days of the month (commonly 1, 2 and 31). Confirm this on your release, as it limits which due dates can be tested. Use the current date in Tally to place them inside the alert window, and note that a licensed copy still needs a pass before a paid release.

## What to re-run

| Change | Run |
|---|---|
| TDL menus or keys | K, S |
| TDL collections or dates | D, A |
| `notify.ps1`, `TDV2Toast.vbs` | A, N |
| Link handlers | N |
| Installer | I, then S |
| License or expiry | L, then S |
| Anything, before release | Everything, on A and B |

## Cases

**S: Smoke** (always)
- S1 TDL loads with no error, and the Gateway shows three items
- S2 Welcome notification on start
- S3 Dashboard's last line shows the expected script path

**K: Keys and menus**
- K1 Ctrl+Y, K2 Ctrl+O, K3 Ctrl+S each work from the Gateway
- K4 Each key does not break the same key's default action on other screens
- K5 All three menu items work with the keys ignored

**D: Dates and windows**
- D1 Nothing in window shows "Nothing to report"
- D2 On the first day and on the last day of the window, included
- D3 One day outside on either side, excluded
- D4 Timeframes of 0 and 365
- D5 Settings saved with Ctrl+A survive a restart
- D6 Reinstall with new values applies them once only

**A: Alerts and data**
- A1 PDC only, A2 invoice only, A3 both
- A4 Exactly 5 and then 6 items, showing "(+1 more)"
- A5 Cancelled and optional vouchers excluded
- A6 Non-Sales voucher types behave as intended
- A7 Party names with `&`, `"`, `'`, `%`, and a long name
- A8 Two companies open; company switch

**N: Notifications**
- N1 Toast shown, N2 pop-up mode, N3 Focus Assist on
- N4 Snooze 1 day and 3 days: task created, Dashboard list updated
- N5 Snoozed reminder fires, including after a restart
- N6 View Details brings TallyPrime forward
- N7 Old toast whose pending file is gone gives no crash

**I: Installer**
- I1 Fresh, I2 upgrade from each old name, I3 repair, I4 uninstall leaves no task or link handler
- I5 Standard user, I6 elevated from another admin account, I7 path with spaces

**L: License**
- L1 Valid key, L2 bad key, L3 no internet at activation, L4 offline within and beyond grace
- L5 Clock moved back, L6 edited license file, L7 license copied to another PC, L8 expired: all paid features stop, and TallyPrime stays usable

## Run log

Save results in `.claude/skills/test-matrix/runs.md`, one row each: date, build or file changed, environment, case, pass or fail, and notes. A pass stays valid until a change in the table above affects it, so skip cases that are still valid and say so.

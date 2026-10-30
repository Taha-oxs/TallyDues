---
name: docs-sync
description: Keep the TallyDue docs in step with the code. Use after changing keys, menus, install steps, trial or license behaviour, file names, or notifications, and before any release, to find what is out of date and update it.
---

# Docs sync

## Docs

| Doc | Status |
|---|---|
| `README.txt` | Editable here |
| `TallyDue-User-Guide.pdf` | Output only: the source file is not in this folder, so list needed edits and ask for the source |
| Third doc | Ask the user which one it is, then record it here |

## Facts to check

The code decides what is true. Read each fact from its source, never from another doc.

| Fact | Source |
|---|---|
| Product name, and TDL file to load | Installer, `tally.ini`, TDL header |
| Shortcut keys and menu items | `[Key: ...]` and `[#Menu: Gateway of Tally]` in the shipped TDL |
| Timeframe limits and defaults | `Install-TallyDue.ps1`, Settings form |
| Trial length and enforcement | `Install-TallyDue.ps1`, `TDV2Toast.vbs` |
| Notification modes and snooze options | `notify.ps1`, protocol handler |
| Log and data locations | Scripts |
| Uninstall steps | Uninstall scripts |

Also check text in Setup pages, TDL messages and Start Menu shortcut names, as customers read those too.

## Known drift to resolve

- The README says `TallyDue` and tells users to load `TallyDue-PublicBeta.tdl`. `tally.ini` loads `TallyDues-PublicBeta-v2.tdl`, whose messages say `TallyDues`.
- The README does not mention snoozing, which v2 has.

## Workflow

1. Work out which file is the shipped version, and don't document an old one.
2. Compare each fact above against each doc.
3. Report mismatches as doc, line, what it says, what the code does.
4. Edit `README.txt` directly when the code is clearly right. If the code looks wrong instead, flag it and leave both alone.
5. Keep customer wording simple, ASCII in `README.txt`, and never describe untested behaviour as working. Check the `test-matrix` run log first.

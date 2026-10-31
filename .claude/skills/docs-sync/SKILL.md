---
name: docs-sync
description: Keep the TallyDues docs in step with the code. Use after changing menus, install steps, license behaviour, file names, or notifications, and before any release, to find what is out of date and update it.
---

# Docs sync

## Docs

| Doc | Status |
|---|---|
| `TallyDues\README.txt` | Editable here, ASCII |
| `TallyDues\TallyDues-User-Guide.md` | Editable here. Setup's Start Menu opens it |
| `TallyDues-Build-and-Release-Guide.md` | Editable here |
| `installer\next-steps.txt` | Editable here |
| `docs\License-Design.md` | Editable here |
| System Design, Code Documentation | In the user's Claude Desktop project, dated 10 Oct 2026 and outdated. Not in this repo |

## Facts to check

The code decides what is true. Read each fact from its source, never from another doc.

| Fact | Source |
|---|---|
| Add-on file name | `installer\TallyDues-Setup.iss` |
| Menu items | `[#Menu: Gateway of Tally]` in `TallyDues.tdl` |
| Timeframe limits and defaults | Settings form in `TallyDues.tdl` |
| License behaviour and messages | `TallyDuesLicense\Program.cs`, `[Code]` in the `.iss` |
| Notification modes and snooze options | `notify.ps1`, `TallyDuesProtocol.ps1` |
| Log and data locations | Scripts |
| Uninstall steps | Uninstall script, `[Code]` in the `.iss` |

Also check text in Setup pages and TDL messages, and Start Menu shortcut names, as customers read those too.

## Known limits to word carefully

Invoices match the voucher date, and paid invoices are included. Don't describe them as "due" without saying so.

## Workflow

1. Compare each fact above against each doc.
2. Report mismatches as doc, line, what it says, what the code does.
3. Edit the doc when the code is clearly right. If the code looks wrong instead, flag it and leave both alone.
4. Keep customer wording simple, and never describe untested behaviour as working. Check the `test-matrix` run log first.

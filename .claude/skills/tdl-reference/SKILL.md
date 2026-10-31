---
name: tdl-reference
description: TDL syntax rules, TallyPrime version differences and known pitfalls for this project. Use before writing, changing, reviewing or debugging any .tdl file, including Gateway menus, System Events, collections and Browse URL calls.
---

# TDL reference

Never guess TDL syntax. Find a working example first, in this order:

1. Local samples: `C:\Program Files\TallyPrimeDeveloper\Samples` (696 files, matched to the installed product). Grep them for the attribute or action.
2. Official docs: https://help.tallysolutions.com/actions-events-buttons-keys-in-tdl/ and https://help.tallysolutions.com/working-of-tally-erp-9-customisations-with-tallyprime/
3. This file.

If none of them confirm a construct, say it is unverified and give the user a small test. Compiling or loading is the only proof: TDL often fails silently.

Evidence labels: **[Doc]** official docs, **[Sample]** local samples, **[Seen]** observed in this project, **[Unverified]** general knowledge to confirm.

## Sample locations

| Topic | Folder under Samples |
|---|---|
| System Events and Timer | `Whats New/Rel 1.x/Rel 1.8/Event Framework/` |
| Browse URL, Exec Command | `Actions in TDL/` |
| Keys, buttons, variables | `Variables & Buttons/` |
| Functions | `User Defined Functions/` |
| Collections, XML/JSON sources | `Objects & Collections/` |
| Forms, parts, sizing | `Dimensions & Formatting/`, `TDL Language Components/` |
| Bell notifications, HTTP JSON | `Whats New/Rel TP.x/Rel TP 7.0/` |

## Syntax rules

- Prefixes: `@` local formula, `@@` global formula, `#` reference to or changes on an existing definition, `##` variable, `$` method, `$$` function. [Doc]
- Change a default definition with `[#Menu: Gateway of Tally]`, not by redefining it. [Sample]
- A system event is `Label : <Event> : <Condition> : <Action> : <Parameters>` under `[System: Events]`. Each label must be unique. [Doc]
- Events: `System Start`, `System End`, `Load Company`, `Close Company`. Samples guard them with `NOT $$IsRemoteCompany`. [Sample]
- Timers are declared as `Name : TIMER : <Condition> : <Action>` under `[System: Event]`, then started with `START TIMER : Name : <seconds>`. [Doc]
- `Display`, `Alter` and `Create` are global actions and can start from a menu, key or field. A `Menu` action can only act on a menu. [Doc]
- Function action numbers are unique and ascending, with gaps left for inserting. Every `If` has `End If`, every `Walk Collection` has `End Walk`. Globals need both `[Variable: X]` and a `[System: Variable]` entry. [Seen]

## TallyPrime differences from Tally.ERP 9

- F1 is reserved for help. F11 is a System Key. F12 is gone from the Menu context. The Bottom Toolbar is gone, and old top toolbar buttons fail to load. [Doc]
- Ctrl+Alt+T opens TDL Management. Import and company options moved to the top toolbar. [Doc]

Check the version first: `(Get-Item 'C:\Program Files\TallyPrime\tally.exe').VersionInfo.ProductVersion` returned 1.1.7.1 on this PC, and the Samples go up to Rel TP 7.1. Say which version a claim applies to.

## Compiling

- Compile to `.tcp` in TallyPrime Developer with **Inscribed V1**, which runs on Release 7. Inscribed V2 needs 7.2. Decompilation is not supported for Inscribed files. [Doc]
- A `.tcp` cannot be changed by the installer, so nothing is stamped. Per-install values are read from a file with `Data Source : File JSON`. [Seen for the snooze board; unverified for a compiled file]

## Project pitfalls

- TDL files must be ASCII. [Seen]
- Menu items are the only supported route; shortcut keys are out of scope. [Seen]
- An empty formula is the suspected cause of Run Check failing silently, so `TDV2Cur` is never `""`. [Seen, cause unconfirmed]
- `TDV2Cur` uses Tally's internal `@@CMPCurrencySymbol`. It exists in the default TDL (`Samples\Supporting Files\InUse\RefCountDumpLog.csv`). [Unverified in Run Check]
- The Dashboard footer reads `$$CollectionField:$LicText:1:TDV2LicFile`. The function is in the Samples, but this use is [Unverified].
- `##SVCurrentCompany` and voucher collections need a loaded company. Use `Load Company` rather than `System Start` for company data. [Unverified]
- Check `tally.ini` for the `TDL=` line before debugging, because it shows which file is really loaded. [Seen]

## Debugging

Logs: `C:\Program Files\TallyPrime\tdlfunc.log`, `tallyerr.log`, and `toast.log` in `C:\ProgramData\TallyDues`. Change one thing at a time and restart TallyPrime after each TDL change.

## Incident log

When a TDL behaviour is confirmed by testing, add it here with the TallyPrime version and date.

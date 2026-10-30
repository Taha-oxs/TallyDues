---
name: tdl-reference
description: TDL syntax rules, TallyPrime version differences and known pitfalls for this project. Use before writing, changing, reviewing or debugging any .tdl file, including key bindings, Gateway menus, System Events, collections and Browse URL calls.
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

## Syntax rules

- Prefixes: `@` local formula, `@@` global formula, `#` reference to or changes on an existing definition, `##` variable, `$` method, `$$` function. [Doc]
- Change a default definition with `[#Menu: Gateway of Tally]`, not by redefining it. [Sample]
- A key is `[Key: Name]` with `Key` and `Action`. It then needs attaching, for example `Add : Keys : Name`. [Doc]
- When one key is defined at several levels, the lowest level wins: a key on a field overrides the same key on the form. [Doc]
- A system event is `Label : <Event> : <Condition> : <Action> : <Parameters>` under `[System: Events]`. Each label must be unique. [Doc]
- Events: `System Start`, `System End`, `Load Company`, `Close Company`. Samples guard them with `NOT $$IsRemoteCompany`. [Sample]
- Timers are declared as `Name : TIMER : <Condition> : <Action>` under `[System: Event]`, then started with `START TIMER : Name : <seconds>`. [Doc]
- `Display`, `Alter` and `Create` are global actions and can start from a menu, key or field. A `Menu` action can only act on a menu. [Doc]

## TallyPrime differences from Tally.ERP 9

- F1 is reserved for help. Buttons bound to keys already used in Default TDL fail. [Doc]
- F11 is a System Key. F12 is gone from the Menu context. The Bottom Toolbar is gone, and old top toolbar buttons fail to load. [Doc]
- Ctrl+Alt+T opens TDL Management. Import and company options moved to the top toolbar. [Doc]
- The docs have no full list of reserved Ctrl keys, so a key can only be cleared by testing it in the target release. [Doc]

Check the version first: `(Get-Item 'C:\Program Files\TallyPrime\tally.exe').VersionInfo.ProductVersion` returned 1.1.7.1 on this PC. Say which version a claim applies to.

## Project pitfalls

- TDL files must be ASCII. `Install-TallyDue.ps1` refuses anything else. [Seen]
- Ctrl+Alt combinations did not respond, so keys use one modifier. Menu items must always exist as a fallback. [Seen]
- An empty formula is the suspected cause of Run Check failing silently, so `TDV2Cur` is never `""`. [Seen, cause unconfirmed]
- `TDV2Script` uses an 8.3 short path because the command line breaks on spaces. [Seen]
- Function line labels are kept ascending here by convention; leave gaps when inserting. [Seen]
- `##SVCurrentCompany` and voucher collections need a loaded company. Use `Load Company` rather than `System Start` for company data. [Unverified]
- Check `tally.ini` for the `TDL=` line before debugging, because it shows which file is really loaded. It currently loads `TallyDues-PublicBeta-v2.tdl`. [Seen]

## Debugging

Logs: `C:\Program Files\TallyPrime\tdlfunc.log`, `tallyerr.log`, and `toast.log` in the install folder (`C:ProgramDataTallyDue`). Change one thing at a time and restart TallyPrime after each TDL change.

## Incident log

When a TDL behaviour is confirmed by testing, add it here with the TallyPrime version and date.

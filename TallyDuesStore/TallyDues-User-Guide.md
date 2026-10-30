# TallyDues - Installation & User Guide

*Licensed edition v1.0  -  TallyPrime add-on*

### Installation & User Guide

TallyDues watches your TallyPrime data and shows Windows notifications for cheques (PDCs) and sales invoices that are due, plus an on-screen dashboard listing them.

**Developed by OpenXpert Solutions**

## What TallyDues does

- **Notifications** - a Windows notification for the cheques / PDCs and invoices that fall in your chosen timeframe (up to 5 of each shown, then "+N more").
- **Snooze** - cheque and invoice notifications have **Snooze 1 day** and **Snooze 3 days** buttons. The reminder comes back when the time is up.
- **Dashboard** - an on-screen list of every due cheque and invoice with number, customer, amount and date, plus the reminders you have snoozed. If nothing is due it says so.
- **Settings** - you decide how many days back (already due) and ahead (coming up) to look, separately for cheques and invoices.
- **Welcome message** - each time TallyPrime starts, a notification confirms TallyDues is loaded and shows your license end date.

## Requirements

- Windows 10 or 11 (64-bit) and TallyPrime Release 7 or later.
- No administrator rights are needed. Everything installs to `C:\ProgramData\TallyDues`. Please leave that folder where it is - TallyPrime reads the add-on from there every time it starts.
- Windows notifications must be allowed (see Troubleshooting).

## If you used the earlier version called TallyDue

The app is now named **TallyDues** and installs to its own folder, `C:\ProgramData\TallyDues`. Uninstall the old version first (Start Menu -> TallyDue -> Uninstall TallyDue) and remove the old TallyDue file from TallyPrime's list (F1 -> TDL and Add-On -> F4), so TallyPrime does not load two copies of the same add-on.

## 1. Install

1. Run `TallyDues-Store-Setup.exe` and click Next through the wizard.
2. On the **License file** page choose the `.lic` file you received from OpenXpert Solutions. Setup checks it straight away and tells you the reason if it cannot be used (a changed file, a file that is not a TallyDues license, or an expired license). If a license is already installed on this PC you may leave the box empty to keep it.
3. On the **Alert timeframe** page enter four whole numbers from 0 to 365 (default 7 each): cheques days back and ahead, invoices days back and ahead. You can change them later inside TallyPrime.
4. Click Install, then read the final page - it repeats the one step below. Windows may show a SmartScreen notice for a new program; choose *More info -> Run anyway* if you trust the source.

## 2. Load TallyDues into TallyPrime (once)

TallyPrime only registers add-ons through its own menu, so this one step cannot be automated:

1. Open TallyPrime.
2. Press **F1 (Help)** -> **TDL and Add-On** -> **F4 (Manage Local TDL Files)**.
3. Set **Load on startup** to **Yes**, browse to `TallyDues-Store-v1.0.tdl` in `C:\ProgramData\TallyDues`, and press **Ctrl+A**.
4. Close and reopen TallyPrime completely. A **Welcome to TallyDues** notification appears.

## 3. Using TallyDues

Open a company and stay on the Gateway of Tally screen. TallyDues adds three items to the Gateway of Tally menu:

| Menu item | What it does |
|---|---|
| **TallyDues Run Check** | Checks the due cheques and invoices now and shows them as Windows notifications. If nothing is due in your timeframe you get a notification saying so. |
| **TallyDues Dashboard** | Opens a screen listing every cheque / PDC and invoice due in your timeframe: number, customer, amount, date, followed by your snoozed reminders. Shows *NO DUES* for an empty list. The last line shows the alert script in use. Esc closes it. |
| **TallyDues Settings** | Shows the four timeframe numbers with their current values. Type a number, press Enter for the next box, then **Ctrl+A** to save. The values are remembered on this PC. |

## Snoozing a reminder

1. When a cheque or invoice notification appears, click **Snooze 1 day** or **Snooze 3 days**. The welcome, "nothing due" and license-expired messages have no snooze buttons.
2. Windows keeps the reminder for you, even if TallyPrime is closed. When the time is up the same message appears again as a **Snoozed reminder** notification (if the PC was off at that time, it appears soon after the PC is next on).
3. The Dashboard shows a **SNOOZED REMINDERS** section: Cheques or Invoices, how many items, how long you snoozed for, and the date it is snoozed until. Entries are cleared automatically once their date has been reached (the next time TallyDues shows a notification).

Snoozing only delays that one notification. Run Check and the Dashboard's due lists are not filtered by what you snoozed, so the items are still listed there until they fall outside your timeframe.

## How the timeframe works

Days are counted from **TallyPrime's current date** (the date you can change with F2), not the Windows clock. For example, with cheques 3 days back and 14 ahead, a cheque dated up to 3 days ago or up to 14 days from now is listed.

- **Cheques / PDCs** are matched on the cheque (instrument) date entered on a bank allocation of any non-Sales voucher.
- **Invoices** are matched on the voucher date of Sales vouchers, not on a credit-period due date, and invoices already paid are not excluded. "Days back" for invoices therefore means "issued in the last N days".
- Cancelled and optional vouchers are ignored.

## Changing the timeframe from the installer

If you reinstall and enter new numbers on the Alert timeframe page, they replace the numbers saved earlier the next time TallyPrime starts.

## 4. Your license and renewing it

Your license file (`.lic`) names your company, your order and an **end date**. The end date appears in the welcome notification and at the bottom of the Dashboard. Until that date everything works. After it, alerts stop and TallyDues shows only a short notice asking you to renew.

To renew, OpenXpert Solutions sends you a new `.lic` file. Open **Start Menu -> TallyDues -> Update License**, choose the file and click Open. The license is checked first; if it cannot be used you are told why and your previous license stays in place. Your alert timeframe is kept. Restart TallyPrime if it is open.

TallyDues also notices if the PC clock has been moved backward to avoid the end date, and shows a notice asking you to correct the date and time.

## Troubleshooting

| Problem | What to do |
|---|---|
| No welcome notification when TallyPrime starts | The add-on is not loaded: repeat step 2 (F1 -> TDL and Add-On -> F4) and restart TallyPrime. Otherwise Windows may be hiding notifications - see the next row. |
| I never see Windows notifications | In Windows Settings -> System -> Notifications: turn notifications **On**, turn **Do Not Disturb / Focus Assist Off**, and make sure **Windows PowerShell** is On in the app list. Meanwhile choose Start Menu -> TallyDues -> **Alerts - also show pop-up boxes**; every alert then also appears as a pop-up box. The other two Alerts shortcuts switch it back. |
| A TallyDues menu item does nothing | Make sure you are on the Gateway of Tally screen with a company open and that the add-on is loaded (step 2). If the Dashboard opens, its last line shows **Alert script in use**, which must be a file that exists on your PC (normally C:\ProgramData\TallyDues\...). If it is not, run Setup again and restart TallyPrime. |
| Clicking Snooze does nothing, or Windows asks for an app to open a "tallydues" link | Show any TallyDues notification once (for example with Run Check) - that re-registers the button handler - then try Snooze on the new notification. If a message says the snooze could not be scheduled, the details are in toast.log in C:\ProgramData\TallyDues. |
| Dashboard says NO DUES but I expected items | Check the dates: the timeframe is counted from TallyPrime's current date (F2). Open TallyDues Settings to see the numbers. Remember invoices are matched on voucher date. |
| Settings numbers do not change | After typing a number press Enter to move on, and finish with Ctrl+A to save. |
| A message says the license has expired | Use Start Menu -> TallyDues -> **Update License** with your renewed `.lic` file, or contact OpenXpert Solutions. |
| A message says TallyDues could not verify a valid license | The installed license is missing or was changed. Use **Update License** with your original `.lic` file, or contact OpenXpert Solutions. |
| A message says the PC clock has moved backward | Correct the Windows date and time. |

## Uninstalling

1. Windows Settings -> Apps, or Start Menu -> TallyDues -> **Uninstall TallyDues**. This also cancels any pending snooze reminders.
2. In TallyPrime press F1 -> TDL and Add-On -> F4, remove the TallyDues file from the list, and restart TallyPrime (this part cannot be done by the uninstaller).

## About

TallyDues is developed by **OpenXpert Solutions**. It reads your TallyPrime data to show reminders and does not send any data anywhere. TallyPrime is a trademark of Tally Solutions Pvt. Ltd.; TallyDues is not affiliated with or endorsed by Tally Solutions.

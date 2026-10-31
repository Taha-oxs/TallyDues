# TallyDues - Installation & User Guide

*v1.0  -  TallyPrime add-on*

### Installation & User Guide

TallyDues watches your TallyPrime data and shows Windows notifications for cheques (PDCs) and sales invoices in your chosen timeframe, plus an on-screen dashboard listing them.

**Developed by OpenXpert Solutions**

## What TallyDues does

- **Notifications** - a Windows notification for the cheques / PDCs and invoices that fall in your chosen timeframe (up to 5 of each shown, then "+N more").
- **Snooze** - cheque and invoice notifications have **Snooze 1 day** and **Snooze 3 days** buttons. The reminder comes back when the time is up.
- **Dashboard** - an on-screen list of every cheque and invoice in your timeframe with number, customer, amount and date, plus the reminders you have snoozed. If nothing is listed it says so.
- **Settings** - you decide how many days back and ahead to look, separately for cheques and invoices.
- **Welcome message** - each time TallyPrime starts, a notification confirms TallyDues is loaded.

## Requirements

- Windows 10 or 11 (64-bit) and TallyPrime Release 7 or later.
- A license key, paid or 15-day trial, from the OpenXpert portal (sign up there, then look under **My licenses**).
- An internet connection during install. After that TallyDues works without internet until your license end date.
- No administrator rights are needed. Everything installs to `C:\ProgramData\TallyDues`. Please leave that folder where it is - TallyPrime reads the add-on from there every time it starts.
- Windows notifications must be allowed (see Troubleshooting).
- One Windows account per PC. Other accounts on the same PC are not supported in this version.

## 1. Install

1. Run `TallyDues-Setup.exe` and click Next.
2. On the **License key** page enter your key, for example `A1B2-C3D4-E5F6-0718-9A0B-1C2D-3E4F-5061`. Setup checks it straight away and installs nothing if the check fails.
3. Click Install, then read the final page - it repeats the one step below.

If you reinstall on a PC that is already licensed, you may leave the key box empty.

## 2. Load TallyDues into TallyPrime (once)

TallyPrime only registers add-ons through its own menu, so this one step cannot be automated:

1. Open TallyPrime.
2. Press **F1 (Help)** -> **TDL and Add-On** -> **F4 (Manage Local TDL Files)**.
3. Set **Load on startup** to **Yes**, browse to the TallyDues file in `C:\ProgramData\TallyDues`, and press **Ctrl+A**.
4. Close and reopen TallyPrime completely. A **Welcome to TallyDues** notification appears.

## 3. Using TallyDues

Open a company and stay on the Gateway of Tally screen. TallyDues adds three items to the Gateway of Tally menu:

| Menu item | What it does |
|---|---|
| **TallyDues Run Check** | Checks cheques and invoices now and shows them as Windows notifications. If nothing falls in your timeframe you get a notification saying so. |
| **TallyDues Dashboard** | Opens a screen listing every cheque / PDC and invoice in your timeframe: number, customer, amount, date, followed by your snoozed reminders. Shows *NO DUES* for an empty list. The last lines show the alert script in use and your license end date. Esc closes it. |
| **TallyDues Settings** | Shows the four timeframe numbers with their current values (7 until you change them). Type a number, press Enter for the next box, then **Ctrl+A** to save. The values are remembered on this PC. |

## Snoozing a reminder

1. When a cheque or invoice notification appears, click **Snooze 1 day** or **Snooze 3 days**. The welcome, "nothing due" and license messages have no snooze buttons.
2. Windows keeps the reminder for you, even if TallyPrime is closed. When the time is up the same message appears again as a **Snoozed reminder** notification (if the PC was off at that time, it appears soon after the PC is next on).
3. The Dashboard shows a **SNOOZED REMINDERS** section: Cheques or Invoices, how many items, how long you snoozed for, and the date it is snoozed until. Entries are cleared automatically once their date has been reached (the next time TallyDues shows a notification).

Snoozing only delays that one notification. Run Check and the Dashboard's lists are not filtered by what you snoozed, so the items are still listed there until they fall outside your timeframe.

## How the timeframe works

Days are counted from **TallyPrime's current date** (the date you can change with F2), not the Windows clock. For example, with cheques 3 days back and 14 ahead, a cheque dated up to 3 days ago or up to 14 days from now is listed.

- **Cheques / PDCs** are matched on the cheque (instrument) date entered on a bank allocation of any non-Sales voucher.
- **Invoices** are matched on the voucher date of Sales vouchers, not on a credit-period due date, and invoices already paid are not excluded. "Days back" for invoices therefore means "issued in the last N days".
- Cancelled and optional vouchers are ignored.

## 4. Your license and renewing it

Your license has an **end date**, shown at the bottom of the Dashboard. Until that date everything works, with or without internet.

- **Before the end date** TallyDues warns you 14, 7 and 1 days ahead, with **Renew** and **Cancel** buttons.
- **After the end date** alerts stop, and TallyDues shows a notice with the same buttons. The Dashboard and Settings stay open.

To renew:

1. Click **Renew**. The OpenXpert portal opens in your browser. If the page does not load, connect this PC to the internet and try again.
2. Renew your subscription on the portal. Your key stays the same.
3. TallyDues checks for the renewal by itself every 30 minutes while this PC is online. A notification tells you when it is done. If you paid in cash, this happens after OpenXpert validates your payment.

**Cancel** only closes the notification. You will see it again next time.

TallyDues also notices if the PC clock has been moved backward, and shows a notice asking you to correct the date and time.

## Moving your license to another PC

Each key has a number of seats, and each PC uses one. Uninstalling frees the seat if the PC is online. Otherwise release the PC on the OpenXpert portal under **My licenses**. Reinstalling on the same PC never uses another seat, and replacing one part, such as a disk, still counts as the same PC.

## Troubleshooting

| Problem | What to do |
|---|---|
| Setup says it could not reach the license server | Check your internet connection and click **Retry**. Nothing has been installed. |
| Setup says the answer could not be trusted | Your network may be redirecting the connection, for example a wifi login page. Sign in to that network or try another one. |
| Setup says the key was not found | Check the key on the portal under **My licenses**. |
| Setup says all seats are in use | Release a PC you no longer use on the portal under **My licenses**. |
| No welcome notification when TallyPrime starts | The add-on is not loaded: repeat step 2 (F1 -> TDL and Add-On -> F4) and restart TallyPrime. Otherwise Windows may be hiding notifications - see the next row. |
| I never see Windows notifications | In Windows Settings -> System -> Notifications: turn notifications **On**, turn **Do Not Disturb / Focus Assist Off**, and make sure **Windows PowerShell** is On in the app list. Meanwhile choose Start Menu -> TallyDues -> **Alerts - also show pop-up boxes**; every alert then also appears as a pop-up box. The other two Alerts shortcuts switch it back. |
| A TallyDues menu item does nothing | Make sure you are on the Gateway of Tally screen with a company open and that the add-on is loaded (step 2). If the Dashboard opens, its last lines show **Alert script in use**, which must be a file that exists on your PC (normally C:\ProgramData\TallyDues\...). If it is not, run Setup again and restart TallyPrime. |
| Clicking a notification button does nothing, or Windows asks for an app to open a "tallydues" link | Show any TallyDues notification once (for example with Run Check) - that re-registers the button handler - then try the button on the new notification. Details are in toast.log in C:\ProgramData\TallyDues. |
| Dashboard says NO DUES but I expected items | Check the dates: the timeframe is counted from TallyPrime's current date (F2). Open TallyDues Settings to see the numbers. Remember invoices are matched on voucher date. |
| Settings numbers do not change | After typing a number press Enter to move on, and finish with Ctrl+A to save. |
| A message says your license ended, but you have renewed | Connect this PC to the internet and wait up to 30 minutes, or click **Renew** again. If you paid in cash, OpenXpert may not have validated the payment yet. |
| A message says TallyDues has no valid license, or that the license belongs to a different PC | Run Setup again and enter your license key. |
| A message says the Windows date looks wrong | Correct the Windows date and time. |

## Uninstalling

1. Windows Settings -> Apps, or Start Menu -> TallyDues -> **Uninstall TallyDues**. This frees your seat when the PC is online, and cancels any pending snooze reminders.
2. In TallyPrime press F1 -> TDL and Add-On -> F4, remove the TallyDues file from the list, and restart TallyPrime (this part cannot be done by the uninstaller).

## About

TallyDues is developed by **OpenXpert Solutions**. It reads your TallyPrime data to show reminders, and that data stays on your PC. Only your license key, hashed identifiers of this PC and the TallyDues version go to the OpenXpert portal, at install, on uninstall and while checking for a renewal. TallyPrime is a trademark of Tally Solutions Pvt. Ltd.; TallyDues is not affiliated with or endorsed by Tally Solutions.

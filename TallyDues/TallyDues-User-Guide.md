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
2. On the **License key** page enter your key, for example `A1B2-C3D4-E5F6-0718-9A0B-1C2D-3E4F-5061`. Setup checks it straight away and installs nothing if the check fails. **Each key works on one PC at a time.**
3. Click Install, then read the final page - it repeats the one step below.

If you run Setup again on a PC that is already licensed, leave the key box empty.

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
| **TallyDues Change License Key** | Opens a window where you enter a new license key. See *Changing your license key* below. |
| **TallyDues Release License** | Opens a window that removes your license from this PC, so you can use the key again, then uninstalls TallyDues. See *Moving your license to another PC* below. |

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

## Changing your license key

Paying after a trial gives you a new key, shown on the OpenXpert portal under **My licenses**. Renewing keeps your key, so renewals need no change here.

1. Connect this PC to the internet.
2. In TallyPrime choose **TallyDues Change License Key** on the Gateway of Tally menu, or use Start Menu -> TallyDues -> **Change License Key**.
3. Paste or type the new key and click **Continue**.
4. Compare the two end dates, then click **Yes** to confirm.

Your current license keeps working until the new key is accepted. The old key is then removed from this PC but not released. To use the old key on another PC, release it first (see below). After 5 wrong keys, wait an hour before trying again. Change the key from the Windows account that installed TallyDues.

## Moving your license to another PC

A license key works on one PC at a time. Release it from TallyDues, not from the website, because only a release from TallyDues removes the license from the PC.

1. Connect the old PC to the internet.
2. In TallyPrime choose **TallyDues Release License** on the Gateway of Tally menu, or use Start Menu -> TallyDues -> **Release License**.
3. Click **Release**, then **Yes**. Once the OpenXpert portal confirms the release, TallyDues asks whether to uninstall it from that PC. Click **Yes**, then **Yes** again in the uninstaller. If the release fails, nothing is released or uninstalled.
4. On the new PC, run Setup and enter the same key.

If the old PC has no internet, or is lost or broken, please call OpenXpert Solutions and ask them to release your license. Replacing one part of a PC, such as a disk, does not affect your license.

## Troubleshooting

| Problem | What to do |
|---|---|
| Setup says it could not reach the license server | Check your internet connection and click **Retry**. Nothing has been installed. |
| Setup says the answer could not be trusted | Your network may be redirecting the connection, for example a wifi login page. Sign in to that network or try another one. |
| Setup says the key was not found | Check the key on the portal under **My licenses**. |
| Setup says the key is active on another PC | Use **TallyDues Release License** on that PC, then try again. If you cannot, call OpenXpert Solutions. |
| No welcome notification when TallyPrime starts | The add-on is not loaded: repeat step 2 (F1 -> TDL and Add-On -> F4) and restart TallyPrime. Otherwise Windows may be hiding notifications - see the next row. |
| I never see Windows notifications | In Windows Settings -> System -> Notifications: turn notifications **On**, turn **Do Not Disturb / Focus Assist Off**, and make sure **Windows PowerShell** is On in the app list. Meanwhile choose Start Menu -> TallyDues -> **Alerts - also show pop-up boxes**; every alert then also appears as a pop-up box. The other two Alerts shortcuts switch it back. |
| A TallyDues menu item does nothing | Make sure you are on the Gateway of Tally screen with a company open and that the add-on is loaded (step 2). If the Dashboard opens, its last lines show **Alert script in use**, which must be a file that exists on your PC (normally C:\ProgramData\TallyDues\...). If it is not, run Setup again and restart TallyPrime. |
| Clicking a notification button does nothing, or Windows asks for an app to open a "tallydues" link | Show any TallyDues notification once (for example with Run Check) - that re-registers the button handler - then try the button on the new notification. Details are in toast.log in C:\ProgramData\TallyDues. |
| Dashboard says NO DUES but I expected items | Check the dates: the timeframe is counted from TallyPrime's current date (F2). Open TallyDues Settings to see the numbers. Remember invoices are matched on voucher date. |
| Settings numbers do not change | After typing a number press Enter to move on, and finish with Ctrl+A to save. |
| A message says your license ended, but you have renewed | Connect this PC to the internet and wait up to 30 minutes, or click **Renew** again. If you paid in cash, OpenXpert may not have validated the payment yet. |
| A message says TallyDues has no valid license, or that the license belongs to a different PC | Enter your key with **TallyDues Change License Key**. If the key is active on another PC, release it there first, or call OpenXpert Solutions. |
| A message says the license records were changed | Contact OpenXpert Solutions. |
| A message says the Windows date looks wrong | Correct the Windows date and time. |

## Uninstalling

1. Windows Settings -> Apps, or Start Menu -> TallyDues -> **Uninstall TallyDues**. A warning asks you to confirm first. Uninstalling releases your license, deletes it from this PC and cancels any pending snooze reminders. You can use the same key again. If this PC has no internet, you can still uninstall, but your key stays tied to this PC until you call OpenXpert Solutions and ask them to release it.
2. In TallyPrime press F1 -> TDL and Add-On -> F4, remove the TallyDues file from the list, and restart TallyPrime (this part cannot be done by the uninstaller).

## About

TallyDues is developed by **OpenXpert Solutions**. It reads your TallyPrime data to show reminders, and that data stays on your PC. Only your license key, hashed identifiers of this PC and the TallyDues version go to the OpenXpert portal, at install, when changing your key and while checking for a renewal. TallyPrime is a trademark of Tally Solutions Pvt. Ltd.; TallyDues is not affiliated with or endorsed by Tally Solutions.

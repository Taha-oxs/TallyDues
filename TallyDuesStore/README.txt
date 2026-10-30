TallyDues - Licensed edition v1.0
Developed by OpenXpert Solutions
==========================================

TallyDues is a TallyPrime add-on that shows Windows notifications for
cheques (PDCs) and sales invoices that are due, plus an on-screen
dashboard that lists them. It runs under a license file (.lic) issued by
OpenXpert Solutions for your order; the license has an end date.

INSTALL
  Run TallyDues-Store-Setup.exe. Choose your license file (.lic) when
  asked (it is checked before anything is installed), then enter your alert timeframe (four numbers, 0-365 days: cheques back/ahead, invoices
  back/ahead). Then do the one step inside TallyPrime that Setup
  describes on its last page (F1 > TDL and Add-On > F4 > load
  TallyDues-Store-v1.0.tdl from C:\ProgramData\TallyDues, then restart
  TallyPrime).

USE (in TallyPrime, at the Gateway of Tally screen)
  TallyDues Run Check   check what is due now (Windows notifications)
  TallyDues Dashboard   list of due cheques/PDCs and invoices, plus the
                        reminders you have snoozed
  TallyDues Settings    change the alert timeframe (Ctrl+A saves)
  These are items on the Gateway of Tally menu.
  The Dashboard's last line shows the alert script in use.

SNOOZE
  Cheque and invoice notifications have "Snooze 1 day" and "Snooze 3 days"
  buttons. The same reminder comes back as a new notification when the
  time is up, and the Dashboard lists it under SNOOZED REMINDERS until
  then. Snoozing only delays that notification: Run Check and the
  Dashboard still list everything that is due.

RENEWING YOUR LICENSE
  Start Menu > TallyDues > Update License, choose the new .lic file. Your
  alert timeframe is kept. Restart TallyPrime if it is open.
  After the end date alerts stop and TallyDues shows only a renewal notice.

UPGRADING FROM "TallyDue" (the earlier name)
  TallyDues installs to its own folder, C:\ProgramData\TallyDues. Uninstall
  the old one first (Start Menu > TallyDue > Uninstall TallyDue) and remove
  the old TallyDue .tdl line in TallyPrime (F1 > TDL and Add-On > F4), so
  TallyPrime does not load two copies of the same add-on.

NOT SEEING NOTIFICATIONS?
  Windows may be hiding them. Turn notifications on, turn Do Not
  Disturb / Focus Assist off, and allow "Windows PowerShell" in
  Settings > System > Notifications. Start Menu > TallyDues > "Alerts -
  also show pop-up boxes" shows every alert as a pop-up box as well.

MORE
  See TallyDues-User-Guide.pdf (Start Menu > TallyDues > User Guide).
  Diagnostic details are written to toast.log and install.log in
  C:\ProgramData\TallyDues - these stay on your PC.

Uninstall: Start Menu > TallyDues > Uninstall TallyDues (this also
cancels any pending snooze reminders), then remove the TallyDues file in
TallyPrime (F1 > TDL and Add-On > F4) and restart it.

Developed by OpenXpert Solutions

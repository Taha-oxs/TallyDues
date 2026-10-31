TallyDues v1.0
Developed by OpenXpert Solutions
==========================================

TallyDues is a TallyPrime add-on that shows Windows notifications for
cheques (PDCs) and sales invoices in your chosen timeframe, plus an on-screen
dashboard that lists them. It needs a license key from the OpenXpert portal,
paid or 15-day trial.

INSTALL
  Run TallyDues-Setup.exe and enter your license key when asked. Setup needs
  an internet connection to check it, and installs nothing if the check fails.
  Then do the one step inside TallyPrime that Setup describes on its last
  page (F1 > TDL and Add-On > F4 > load the TallyDues file from
  C:\ProgramData\TallyDues, then restart TallyPrime).

USE (in TallyPrime, at the Gateway of Tally screen)
  TallyDues Run Check   check what is due now (Windows notifications)
  TallyDues Dashboard   list of due cheques/PDCs and invoices, plus the
                        reminders you have snoozed
  TallyDues Settings    change the alert timeframe (Ctrl+A saves)
  These are items on the Gateway of Tally menu.
  The Dashboard's last lines show the alert script in use and your license
  end date.

  Invoices are matched on the voucher date, not a credit-period due date,
  and invoices already paid are still listed.

SNOOZE
  Cheque and invoice notifications have "Snooze 1 day" and "Snooze 3 days"
  buttons. The same reminder comes back as a new notification when the
  time is up, and the Dashboard lists it under SNOOZED REMINDERS until
  then. Snoozing only delays that notification: Run Check and the
  Dashboard still list everything that is due.

RENEWING YOUR LICENSE
  After install TallyDues works without internet until your license end date.
  It warns you 14, 7 and 1 days before that date. Click Renew on the
  notification and renew on the OpenXpert portal in your browser. Once this PC
  is online, TallyDues finds the renewal by itself. Your key stays the same.
  After the end date alerts stop until you renew. The Dashboard and Settings
  stay open.

MOVING TO ANOTHER PC
  Uninstall TallyDues on the old PC while it is online, which frees its seat.
  If that PC is offline or gone, release it on the OpenXpert portal under
  My licenses. Then install on the new PC with the same key.

NOT SEEING NOTIFICATIONS?
  Windows may be hiding them. Turn notifications on, turn Do Not
  Disturb / Focus Assist off, and allow "Windows PowerShell" in
  Settings > System > Notifications. Start Menu > TallyDues > "Alerts -
  also show pop-up boxes" shows every alert as a pop-up box as well.

PRIVACY
  Your TallyPrime data stays on this PC. Only your license key, hashed
  identifiers of this PC and the TallyDues version go to the OpenXpert portal,
  at install, on uninstall and while checking for a renewal.

MORE
  See TallyDues-User-Guide.md (Start Menu > TallyDues > User Guide).
  Diagnostic details are written to toast.log and install.log in
  C:\ProgramData\TallyDues - these stay on your PC.

Uninstall: Start Menu > TallyDues > Uninstall TallyDues (this also
cancels any pending snooze reminders), then remove the TallyDues file in
TallyPrime (F1 > TDL and Add-On > F4) and restart it.

Developed by OpenXpert Solutions

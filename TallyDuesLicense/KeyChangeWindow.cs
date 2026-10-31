// The Change License Key window. The key is typed here, kept masked and never passed to anything
// but KeyChange. Left out of test builds: the tests run KeyChange directly.
#if !TEST_BUILD
using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TallyDues.License
{
    internal sealed class KeyChangeWindow : Form
    {
        private readonly KeyChange change;
        private readonly TextBox keyBox = new TextBox();
        private readonly Label status = new Label();
        private readonly Button continueButton = new Button();
        private readonly Button cancelButton = new Button();
        private bool busy;
        private bool changed;
        private string lastTyped = "";

        // One window at a time, so two changes can never run together.
        public static int ShowWindow(string dir)
        {
            bool created;
            using (var single = new Mutex(true, @"Local\TallyDues-KeyChange", out created))
            {
                if (!created) return ExitCodes.NoChange;
                int result = ExitCodes.NoChange;
                var thread = new Thread(() =>
                {
                    Application.EnableVisualStyles();
                    using (var window = new KeyChangeWindow(dir))
                    {
                        Application.Run(window);
                        if (window.changed) result = ExitCodes.Ok;
                    }
                });
                // The clipboard needs a single-threaded apartment.
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
                return result;
            }
        }

        private KeyChangeWindow(string dir)
        {
            change = new KeyChange(dir);
            Text = "TallyDues - Change License Key";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(460, 210);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            // Started from TallyPrime, which would otherwise cover it.
            TopMost = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (ArgumentException) { }

            var intro = new Label
            {
                Text = "Enter your new license key from the OpenXpert portal (My licenses). It needs an internet connection. Your current license stays in place until the new key is accepted.",
                Location = new Point(16, 14),
                Size = new Size(428, 52)
            };
            keyBox.Location = new Point(16, 72);
            keyBox.Size = new Size(428, 25);
            keyBox.MaxLength = 64;
            // A password box also refuses copy and cut. Paste still works.
            keyBox.UseSystemPasswordChar = true;
            status.Location = new Point(16, 104);
            status.Size = new Size(428, 62);
            continueButton.Text = "Continue";
            continueButton.Location = new Point(268, 172);
            continueButton.Size = new Size(84, 28);
            continueButton.Click += OnContinue;
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(360, 172);
            cancelButton.Size = new Size(84, 28);
            cancelButton.Click += (sender, e) => Close();
            AcceptButton = continueButton;
            CancelButton = cancelButton;
            Controls.AddRange(new Control[] { intro, keyBox, status, continueButton, cancelButton });
            FormClosing += OnClosing;
        }

        private async void OnContinue(object sender, EventArgs e)
        {
            string typed = keyBox.Text;
            lastTyped = typed;
            SetBusy(true, "Checking the key with the OpenXpert portal...");
            int code = await Task.Run(() => change.Preview(typed));
            if (code != ExitCodes.Ok)
            {
                SetBusy(false, KeyChange.Message(code));
                keyBox.SelectAll();
                keyBox.Focus();
                return;
            }
            if (MessageBox.Show(this, ConfirmText(), Text, MessageBoxButtons.YesNo, change.NewEnd < change.CurrentEnd ? MessageBoxIcon.Warning : MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                SetBusy(false, "Nothing was changed.");
                return;
            }
            SetBusy(true, "Changing the license key...");
            code = await Task.Run(() => change.Apply());
            if (code != ExitCodes.Ok)
            {
                SetBusy(false, KeyChange.Message(code));
                return;
            }
            changed = true;
            SetBusy(false, "");
            string done = "Your new license key is active until " + ShowDate(change.NewEnd) + ".";
            MessageBox.Show(this, done, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private string ConfirmText()
        {
            string text = "Use the new license key ending in " + change.NewKeyEnding + "?\r\n\r\n";
            text += change.CurrentEnd.HasValue ? "Current license ends: " + ShowDate(change.CurrentEnd.Value) + "\r\n" : "This PC has no valid license now.\r\n";
            text += "New license ends: " + ShowDate(change.NewEnd) + "\r\n\r\n";
            if (change.NewEnd < change.CurrentEnd) text += "WARNING: the new license ends EARLIER than your current one.\r\n\r\n";
            text += "A license key works only once. Clicking Yes uses up the new key, and the old key is removed from this PC and cannot be used again.";
            return text;
        }

        private void SetBusy(bool isBusy, string message)
        {
            busy = isBusy;
            keyBox.Enabled = !isBusy;
            continueButton.Enabled = !isBusy;
            cancelButton.Enabled = !isBusy;
            status.Text = message;
        }

        // A change in progress must finish, so that the burned key and the saved license agree.
        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true;
                return;
            }
            ClearClipboard();
            keyBox.Clear();
        }

        // Only removes the key itself, not whatever else the user copied.
        private void ClearClipboard()
        {
            try
            {
                string typed = Program.NormalizeKey(lastTyped);
                if (typed != null && Clipboard.ContainsText() && Program.NormalizeKey(Clipboard.GetText()) == typed) Clipboard.Clear();
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // Another program holds the clipboard.
            }
        }

        private static string ShowDate(DateTimeOffset date)
        {
            return date.LocalDateTime.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
        }
    }
}
#endif

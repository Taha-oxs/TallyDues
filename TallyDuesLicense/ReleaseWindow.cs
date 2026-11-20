// The Release License window. Left out of test builds: the tests run LicenseRelease directly.
// A release also uninstalls TallyDues. The uninstaller starts only after the portal's signed answer
// confirms the release and the license file is gone from this PC. Any failure uninstalls nothing.
#if !TEST_BUILD
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TallyDues.License
{
    internal sealed class ReleaseWindow : Form
    {
        private readonly string dir;
        private readonly Label status = new Label();
        private readonly Button releaseButton = new Button();
        private readonly Button cancelButton = new Button();
        private bool busy;
        private bool released;

        // Shares the key change's lock, so a release and a key change can never run together.
        public static int ShowWindow(string dir)
        {
            bool created;
            using (var single = new Mutex(true, @"Local\TallyDues-KeyChange", out created))
            {
                if (!created) return ExitCodes.NoChange;
                int result = ExitCodes.NoChange;
                Application.EnableVisualStyles();
                using (var window = new ReleaseWindow(dir))
                {
                    // TallyPrime starts the helper hidden, and Windows applies that to the first show only.
                    window.Show();
                    window.Hide();
                    Application.Run(window);
                    if (window.released) result = ExitCodes.Ok;
                }
                return result;
            }
        }

        private ReleaseWindow(string installDir)
        {
            dir = installDir;
            Text = "TallyDues - Release License";
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
                Text = "Releasing removes your license from this PC and then uninstalls TallyDues. You can then use the same key on this PC or another one. It needs an internet connection.",
                Location = new Point(16, 14),
                Size = new Size(428, 52)
            };
            status.Location = new Point(16, 72);
            status.Size = new Size(428, 94);
            releaseButton.Text = "Release";
            releaseButton.Location = new Point(268, 172);
            releaseButton.Size = new Size(84, 28);
            releaseButton.Click += OnRelease;
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(360, 172);
            cancelButton.Size = new Size(84, 28);
            cancelButton.Click += (sender, e) => Close();
            AcceptButton = cancelButton;
            CancelButton = cancelButton;
            Controls.AddRange(new Control[] { intro, status, releaseButton, cancelButton });
            FormClosing += (sender, e) => e.Cancel = busy;
        }

        private async void OnRelease(object sender, EventArgs e)
        {
            string question = "Release your TallyDues license and uninstall TallyDues from this PC?\r\n\r\n" +
                "The license server must confirm the release first. If it does not, nothing is released or uninstalled.";
            if (MessageBox.Show(this, question, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            SetBusy(true, "Releasing your license...");
            int code = await Task.Run(() => Program.Guarded(new[] { "release", "--install-dir", dir }));
            SetBusy(false, "");
            if (code != ExitCodes.Ok)
            {
                status.Text = LicenseRelease.Message(code) + " TallyDues was not uninstalled.";
                return;
            }
            released = true;
            // Ok can also mean a new key arrived during the portal call and its license stayed. Keep TallyDues then.
            if (File.Exists(Path.Combine(dir, "license.dat")))
            {
                string kept = "The old license is released, but a new license key is now in place on this PC, so TallyDues was not uninstalled.";
                MessageBox.Show(this, kept, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }
            string uninstaller = FindUninstaller();
            if (uninstaller == null)
            {
                string manual = "Your license is released. The TallyDues uninstaller was not found, so please uninstall TallyDues from Windows Settings > Apps.";
                MessageBox.Show(this, manual, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }
            string done = "The license server confirmed that your license is released.\r\n\r\n" +
                "Uninstall TallyDues from this PC now?\r\n\r\n" +
                "If you choose No, TallyDues stays installed without a license. You can uninstall it later from Start Menu > TallyDues > Uninstall TallyDues.";
            if (MessageBox.Show(this, done, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
            {
                try
                {
                    // Not waited on: the uninstaller deletes this folder, including this exe, once it closes.
                    Process.Start(new ProcessStartInfo(uninstaller) { UseShellExecute = true, WorkingDirectory = Path.GetTempPath() });
                }
                catch (Exception)
                {
                    string failed = "Your license is released, but the uninstaller could not start. Please uninstall TallyDues from Windows Settings > Apps.";
                    MessageBox.Show(this, failed, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            Close();
        }

        // Inno Setup names its uninstaller unins000.exe, next to unins000.dat, in the install folder.
        private string FindUninstaller()
        {
            try
            {
                string[] found = Directory.GetFiles(dir, "unins???.exe");
                Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                foreach (string exe in found)
                {
                    if (File.Exists(Path.ChangeExtension(exe, ".dat"))) return exe;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return null;
        }

        private void SetBusy(bool isBusy, string message)
        {
            busy = isBusy;
            releaseButton.Enabled = !isBusy;
            cancelButton.Enabled = !isBusy;
            status.Text = message;
        }
    }
}
#endif

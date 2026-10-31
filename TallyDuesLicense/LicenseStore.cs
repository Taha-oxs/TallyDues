// Files in the install folder: the signed license, the unsigned state and the Dashboard text.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace TallyDues.License
{
    internal sealed class LicenseFile
    {
        public string KeyProtected { get; set; }
        public string[] Components { get; set; }
        public string Random { get; set; }
        public string PayloadRaw { get; set; }
        public string Signature { get; set; }

        public DateTimeOffset ValidUntil()
        {
            var payload = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(PayloadRaw);
            return DateTimeOffset.Parse(Portal.AsString(payload, "valid_until"), CultureInfo.InvariantCulture);
        }

        // Signed by the portal, for this nonce, and the stored machine hashes produce that nonce.
        public bool IsAuthentic()
        {
            if (Components == null || Components.Length != 4 || string.IsNullOrEmpty(Random)) return false;
            if (string.IsNullOrEmpty(PayloadRaw) || !Portal.IsTrusted(PayloadRaw, Signature)) return false;
            var payload = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(PayloadRaw);
            // A validate answer holds a confirm token and never counts as a license: it takes no seat.
            return Portal.AsString(payload, "result") == "ok"
                && !payload.ContainsKey("confirm_token")
                && Portal.AsString(payload, "valid_until").Length > 0
                && Portal.AsString(payload, "nonce") == Machine.Nonce(Components, Random);
        }
    }

    internal sealed class LicenseStore
    {
        private readonly string dir;

        public LicenseStore(string installDir) { dir = installDir; }

        private string LicensePath { get { return Path.Combine(dir, "license.dat"); } }
        private string StatePath { get { return Path.Combine(dir, "state.dat"); } }
        private string StatusPath { get { return Path.Combine(dir, "license-status.json"); } }

        public LicenseFile Load()
        {
            try
            {
                return new JavaScriptSerializer().Deserialize<LicenseFile>(File.ReadAllText(LicensePath, Encoding.ASCII));
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Save(LicenseFile license)
        {
            Directory.CreateDirectory(dir);
            WriteAtomic(LicensePath, new JavaScriptSerializer().Serialize(license));
        }

        public static string Protect(string key)
        {
            byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        public static string Unprotect(string stored)
        {
            byte[] data = ProtectedData.Unprotect(Convert.FromBase64String(stored), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }

        public Dictionary<string, string> LoadState()
        {
            var state = new Dictionary<string, string>();
            try
            {
                foreach (string line in File.ReadAllLines(StatePath, Encoding.ASCII))
                {
                    int split = line.IndexOf('=');
                    if (split > 0) state[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
                }
            }
            catch (IOException)
            {
                // First run: there is no state yet.
            }
            catch (UnauthorizedAccessException)
            {
                // Treated the same as no state.
            }
            return state;
        }

        // State is a helper, not the license: failing to write it must not cost the customer an alert.
        public void SaveState(Dictionary<string, string> state)
        {
            try
            {
                WriteAtomic(StatePath, string.Join("\r\n", state.Select(pair => pair.Key + "=" + pair.Value)));
            }
            catch (IOException)
            {
                // Tried again on the next run.
            }
            catch (UnauthorizedAccessException)
            {
                // Tried again on the next run.
            }
        }

        // The Dashboard footer reads this, so the TDL holds no license date.
        public void WriteStatus(string text)
        {
            string json = new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "Status", new[] { new Dictionary<string, string> { { "LicText", text } } } }
            });
            try
            {
                WriteAtomic(StatusPath, json);
            }
            catch (IOException)
            {
                // Display only: never let it affect the license check.
            }
            catch (UnauthorizedAccessException)
            {
                // Display only.
            }
        }

        private static void WriteAtomic(string path, string text)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, text, Encoding.ASCII);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}

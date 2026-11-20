// Files in the install folder: the signed license, the state and the Dashboard text.
// license.dat and state.dat carry an HMAC seal, so edits to them are refused. The seal's secret
// is on this PC, so it stops casual edits only.
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
        public string Seal { get; set; }

        public string SealedText()
        {
            return string.Join("\n", KeyProtected, Components == null ? "" : string.Join(",", Components), Random, PayloadRaw, Signature);
        }

        public DateTimeOffset ValidUntil()
        {
            var payload = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(PayloadRaw);
            return DateTimeOffset.Parse(Portal.AsString(payload, "valid_until"), CultureInfo.InvariantCulture);
        }

        // Who the portal says holds this license. Empty if its answer does not name them yet.
        public string Licensee()
        {
            var payload = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(PayloadRaw);
            string name = new string(Portal.AsString(payload, "organisation").Where(c => !char.IsControl(c)).ToArray()).Trim();
            return name.Length > 80 ? name.Substring(0, 80).Trim() : name;
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
        private string PendingPath { get { return Path.Combine(dir, "license.pending"); } }
        private string SealKeyPath { get { return Path.Combine(dir, "seal.key"); } }

        // Set by LoadState when state.dat fails its seal.
        public bool StateTampered { get; private set; }

        public LicenseFile Load()
        {
            try
            {
                LicenseFile license = new JavaScriptSerializer().Deserialize<LicenseFile>(File.ReadAllText(LicensePath, Encoding.ASCII));
                return license != null && SealMatches(license.SealedText(), license.Seal) ? license : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Save(LicenseFile license)
        {
            Directory.CreateDirectory(dir);
            license.Seal = SealOf(license.SealedText());
            WriteAtomic(LicensePath, AsciiJson(new JavaScriptSerializer().Serialize(license)));
            RemovePending();
        }

        // The keyfile comes before the portal call: a seat is taken only once this PC has shown it
        // can keep the license. It does not count as a license until the portal's signed answer arrives.
        public bool WritePending(string key, string[] machine, string random)
        {
            try
            {
                Directory.CreateDirectory(dir);
                if (!LicenseCanBeReplaced()) return false;
                var pending = new LicenseFile { KeyProtected = Protect(key), Components = machine, Random = random };
                pending.Seal = SealOf(pending.SealedText());
                string text = new JavaScriptSerializer().Serialize(pending);
                WriteAtomic(PendingPath, text);
                return File.ReadAllText(PendingPath, Encoding.ASCII) == text;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public void RemovePending()
        {
            try
            {
                File.Delete(PendingPath);
            }
            catch (IOException)
            {
                // Replaced by the next try, and removed on uninstall.
            }
            catch (UnauthorizedAccessException)
            {
                // Replaced by the next try, and removed on uninstall.
            }
        }

        // After the portal has freed the seat. The latest date seen goes too, like a new license.
        public void RemoveLicense()
        {
            File.Delete(LicensePath);
            RemovePending();
            SaveState(new Dictionary<string, string>());
            WriteStatus("No license on this PC");
        }

        public bool LicenseCanBeReplaced()
        {
            if (Directory.Exists(LicensePath)) return false;
            if (!File.Exists(LicensePath)) return true;
            using (new FileStream(LicensePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite)) return true;
        }

        private string SealOf(string text)
        {
            using (var hmac = new HMACSHA256(SealSecret()))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
        }

        private bool SealMatches(string text, string seal)
        {
            if (string.IsNullOrEmpty(seal) || !File.Exists(SealKeyPath)) return false;
            string expected = SealOf(text);
            if (expected.Length != seal.Length) return false;
            int difference = 0;
            for (int i = 0; i < seal.Length; i++) difference |= expected[i] ^ seal[i];
            return difference == 0;
        }

        // Machine scope, because Setup and the alerts can run under different accounts on one PC.
        private byte[] SealSecret()
        {
            if (File.Exists(SealKeyPath))
            {
                return ProtectedData.Unprotect(Convert.FromBase64String(File.ReadAllText(SealKeyPath, Encoding.ASCII)), null, DataProtectionScope.LocalMachine);
            }
            byte[] secret = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(secret);
            string temp = SealKeyPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, Convert.ToBase64String(ProtectedData.Protect(secret, null, DataProtectionScope.LocalMachine)), Encoding.ASCII);
                File.Move(temp, SealKeyPath);
                return secret;
            }
            catch (IOException)
            {
                // Another process wrote it first, so use that one.
                if (!File.Exists(SealKeyPath)) throw;
                return SealSecret();
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }

        private string StateText(Dictionary<string, string> state)
        {
            return string.Join("\r\n", state.Where(pair => pair.Key != "SEAL").OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "=" + pair.Value));
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
            StateTampered = false;
            try
            {
                foreach (string line in File.ReadAllLines(StatePath, Encoding.ASCII))
                {
                    int split = line.IndexOf('=');
                    if (split > 0) state[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
                }
                string seal;
                state.TryGetValue("SEAL", out seal);
                StateTampered = !SealMatches(StateText(state), seal);
                state.Remove("SEAL");
            }
            catch (CryptographicException)
            {
                StateTampered = true;
            }
            catch (FormatException)
            {
                StateTampered = true;
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
                string text = StateText(state);
                WriteAtomic(StatePath, (text.Length > 0 ? text + "\r\n" : "") + "SEAL=" + SealOf(text));
            }
            catch (CryptographicException)
            {
                // Tried again on the next run.
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
        public void WriteStatus(string text, string licensee = "")
        {
            string json = AsciiJson(new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "Status", new[] { new Dictionary<string, string> { { "LicText", text }, { "LicTo", licensee } } } }
            }));
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

        // These files are ASCII. Escaping keeps non-English names, and the signed text, unchanged.
        private static string AsciiJson(string json)
        {
            var result = new StringBuilder(json.Length);
            foreach (char c in json)
            {
                if (c < 128) result.Append(c);
                else result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
            }
            return result.ToString();
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

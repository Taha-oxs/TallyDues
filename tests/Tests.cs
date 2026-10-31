// Automated tests for the license helper, with a mock portal that answers like the real one.
// Run with build\Run-Tests.ps1. Nothing here touches the real install folder or a real portal.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace TallyDues.License.Tests
{
    internal static class Hex
    {
        public static byte[] Parse(string hex)
        {
            if (hex == "-") return new byte[0];
            return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
        }
    }

    internal sealed class MockPortal
    {
        public const int Port = 18765;
        public const string GoodKey = "A1B2-C3D4-E5F6-0718-9A0B-1C2D-3E4F-5061";

        public string Mode = "ok";
        public string ValidUntil = "2027-04-30T10:00:00+00:00";
        public string ErrorCode;
        public int Calls;
        public int ConfirmFailures;
        public int Burns;
        public int ReleaseCalls;
        public bool LoseNextConfirm;
        private bool burnedAndLost;
        public string ConfirmError;
        public Action OnHeartbeat;

        private readonly byte[] seed = Enumerable.Repeat((byte)7, 32).ToArray();
        private readonly byte[] otherSeed = Enumerable.Repeat((byte)9, 32).ToArray();
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, Port);

        public string PublicKey { get { return Convert.ToBase64String(Ed25519.PublicFromSeed(seed)); } }

        public void Start()
        {
            listener.Start();
            new Thread(Loop) { IsBackground = true }.Start();
        }

        private void Loop()
        {
            while (true)
            {
                using (TcpClient client = listener.AcceptTcpClient())
                {
                    try { Handle(client); } catch (IOException) { }
                }
            }
        }

        private void Handle(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            string head = "";
            var buffer = new List<byte>();
            while (!head.EndsWith("\r\n\r\n"))
            {
                int b = stream.ReadByte();
                if (b < 0) return;
                buffer.Add((byte)b);
                head = Encoding.ASCII.GetString(buffer.ToArray());
            }
            string lengthLine = head.Split(new[] { "\r\n" }, StringSplitOptions.None).First(l => l.StartsWith("Content-Length", StringComparison.OrdinalIgnoreCase));
            int length = int.Parse(lengthLine.Split(':')[1].Trim());
            byte[] body = new byte[length];
            int read = 0;
            while (read < length) read += stream.Read(body, read, length - read);
            string path = head.Split(' ')[1];
            Calls++;
            if (Mode == "drop") return;
            if (path.EndsWith("/confirm") && LoseNextConfirm)
            {
                // The portal burns the key, but its answer never arrives.
                LoseNextConfirm = false;
                burnedAndLost = true;
                Burns++;
                return;
            }
            string status, json;
            Answer(path, new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Encoding.UTF8.GetString(body)), out status, out json);
            byte[] output = Encoding.UTF8.GetBytes(json);
            string headers = "HTTP/1.1 " + status + "\r\nContent-Type: application/json\r\nConnection: close\r\nContent-Length: " + output.Length + "\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(output, 0, output.Length);
        }

        private void Answer(string path, Dictionary<string, object> request, out string status, out string json)
        {
            status = "200 OK";
            if (Mode == "http500") { status = "500 Server Error"; json = "oops"; return; }
            if (Mode == "throttle") { status = "429 Too Many Requests"; json = "{\"message\":\"Too Many Attempts.\"}"; return; }
            if (Mode == "redirect") { status = "302 Found"; json = "{}"; return; }
            if (Mode == "html") { json = "<html>captive wifi page</html>"; return; }
            if (ErrorCode != null) { status = ErrorCode == "invalid_key" ? "404 Not Found" : ErrorCode == "rate_limited" ? "429 Too Many" : "403 Forbidden"; json = "{\"error\":\"" + ErrorCode + "\"}"; return; }
            if (path.EndsWith("/confirm") && ConfirmError != null) { status = "403 Forbidden"; json = "{\"error\":\"" + ConfirmError + "\"}"; return; }
            if (path.EndsWith("/heartbeat") && OnHeartbeat != null) OnHeartbeat();
            string nonce = request["nonce"].ToString();
            if (Mode == "wrong-nonce") nonce = new string('a', 64);
            string payload;
            if (path.EndsWith("/validate"))
            {
                payload = "{\"result\":\"ok\",\"nonce\":\"" + nonce + "\",\"confirm_token\":\"" + new string('t', 48) + "\",\"seats_free\":1,\"valid_until\":\"" + ValidUntil + "\"}";
            }
            else if (path.EndsWith("/confirm") && ConfirmFailures > 0)
            {
                ConfirmFailures--;
                status = "404 Not Found";
                json = "{\"error\":\"invalid_key\"}";
                return;
            }
            else if (path.EndsWith("/release"))
            {
                ReleaseCalls++;
                payload = "{\"result\":\"ok\",\"nonce\":\"" + nonce + "\"}";
            }
            else
            {
                // A repeated confirm after a lost answer must not burn twice.
                if (path.EndsWith("/confirm")) { if (burnedAndLost) burnedAndLost = false; else Burns++; }
                payload = "{\"result\":\"ok\",\"nonce\":\"" + nonce + "\",\"valid_until\":\"" + ValidUntil + "\"}";
            }
            byte[] signature = Ed25519.Sign(Mode == "wrong-key" ? otherSeed : seed, Encoding.UTF8.GetBytes(payload));
            string sent = Mode == "tamper" ? payload.Replace("2027", "2099") : payload;
            json = Mode == "unsigned"
                ? "{\"payload\":" + sent + "}"
                : "{\"payload\":" + sent + ",\"signature\":\"" + Convert.ToBase64String(signature) + "\"}";
        }
    }

    internal static class Runner
    {
        private static int failed, passed;
        private static MockPortal portal;
        private static string root, dir, keyFile, stateFile, outFile;

        private static int Main(string[] args)
        {
            root = Path.Combine(Path.GetTempPath(), "tallydues-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                portal = new MockPortal();
                portal.Start();
                Environment.SetEnvironmentVariable("TD_TEST_PUBLIC_KEY", portal.PublicKey);
                keyFile = Path.Combine(root, "key.txt");
                stateFile = Path.Combine(root, "state.txt");
                outFile = Path.Combine(root, "out.txt");

                Environment.SetEnvironmentVariable("TD_TEST_MACHINE", null);
                string[] real = Machine.Components();
                Check("real PC gives 4 different hashes", real.Length == 4 && real.Distinct().Count() == 4 && real.All(c => c.Length == 64));
                Check("real PC hashes are stable", real.SequenceEqual(Machine.Components()));

                SignatureVectors(args[0]);
                KeyFormat();
                HappyPath();
                PortalErrors();
                Outages();
                ForgedAnswers();
                ConfirmEdges();
                TamperedFiles();
                MachineChanges();
                ClockChecks();
                Warnings();
                Renewal();
                OneUseKeys();
                ReviewFixes();
                KeyChanges();
            }
            finally
            {
                Directory.Delete(root, true);
            }
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        private static void SignatureVectors(string vectorFile)
        {
            byte[] rfcSeed = Hex.Parse("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");
            byte[] rfcPublic = Hex.Parse("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a");
            byte[] rfcSignature = Hex.Parse("e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b");
            Check("RFC 8032 public key", Ed25519.PublicFromSeed(rfcSeed).SequenceEqual(rfcPublic));
            Check("RFC 8032 signature", Ed25519.Sign(rfcSeed, new byte[0]).SequenceEqual(rfcSignature));
            Check("RFC 8032 verifies", Ed25519.Verify(rfcPublic, new byte[0], rfcSignature));

            foreach (string line in File.ReadAllLines(vectorFile).Where(l => l.Length > 0))
            {
                string[] parts = line.Split(' ');
                byte[] key = Hex.Parse(parts[0]), message = Hex.Parse(parts[1]), signature = Hex.Parse(parts[2]);
                Check("Node.js signature verifies", Ed25519.Verify(key, message, signature));
                byte[] bad = (byte[])signature.Clone();
                bad[5] ^= 1;
                Check("changed signature fails", !Ed25519.Verify(key, message, bad));
                byte[] changed = message.Concat(new byte[] { 1 }).ToArray();
                Check("changed message fails", !Ed25519.Verify(key, changed, signature));
            }
            Check("short signature fails", !Ed25519.Verify(rfcPublic, new byte[0], new byte[63]));
            byte[] highS = (byte[])rfcSignature.Clone();
            for (int i = 32; i < 64; i++) highS[i] = 0xFF;
            Check("oversized S fails", !Ed25519.Verify(rfcPublic, new byte[0], highS));
        }

        private static void KeyFormat()
        {
            Reset();
            int before = portal.Calls;
            File.WriteAllText(keyFile, "not-a-key");
            Check("bad format rejected", Run("validate") == ExitCodes.InvalidKey);
            Check("bad format sends nothing", portal.Calls == before);
        }

        private static void HappyPath()
        {
            Reset();
            int burns = portal.Burns;
            Check("validate ok", Run("validate") == ExitCodes.Ok);
            Check("validate burns nothing", portal.Burns == burns);
            Check("no license before confirm", Run("check") == ExitCodes.NoLicense);
            Check("confirm ok", Run("confirm") == ExitCodes.Ok);
            Check("confirm burns the key", portal.Burns == burns + 1);
            Check("keyfile becomes the license", !File.Exists(Path.Combine(dir, "license.pending")));
            Check("check ok", Run("check") == ExitCodes.Ok);
            string stored = File.ReadAllText(Path.Combine(dir, "license.dat"));
            Check("key not stored in plain text", !stored.Contains(MockPortal.GoodKey));
            Check("status file written", File.ReadAllText(Path.Combine(dir, "license-status.json")).Contains("30-Apr-2027"));
        }

        private static void PortalErrors()
        {
            var expected = new Dictionary<string, int>
            {
                { "invalid_key", ExitCodes.InvalidKey }, { "expired", ExitCodes.KeyExpired }, { "revoked", ExitCodes.Revoked },
                { "subscription_suspended", ExitCodes.Suspended }, { "limit_reached", ExitCodes.KeyUsed }, { "key_used", ExitCodes.KeyUsed }, { "rate_limited", ExitCodes.RateLimited }
            };
            foreach (var pair in expected)
            {
                Reset();
                portal.ErrorCode = pair.Key;
                Check("error " + pair.Key, Run("validate") == pair.Value);
            }
            portal.ErrorCode = null;
        }

        private static void Outages()
        {
            foreach (string mode in new[] { "drop", "http500" })
            {
                Reset();
                portal.Mode = mode;
                Check("network message for " + mode, Run("validate") == ExitCodes.Network);
            }
            foreach (string mode in new[] { "redirect", "html" })
            {
                Reset();
                portal.Mode = mode;
                Check("bad response for " + mode, Run("validate") == ExitCodes.BadResponse);
            }
            portal.Mode = "ok";
        }

        private static void ForgedAnswers()
        {
            foreach (string mode in new[] { "tamper", "wrong-nonce", "unsigned", "wrong-key" })
            {
                Reset();
                Run("validate");
                portal.Mode = mode;
                Check("rejected at confirm: " + mode, Run("confirm") == ExitCodes.BadResponse);
                Check("nothing stored: " + mode, !File.Exists(Path.Combine(dir, "license.dat")));
                Check("rejected at validate: " + mode, Run("validate") == ExitCodes.BadResponse);
            }
            portal.Mode = "ok";
        }

        private static void ConfirmEdges()
        {
            Reset();
            Run("validate");
            portal.ConfirmFailures = 1;
            Check("old token validates again", Run("confirm") == ExitCodes.Ok);

            Reset();
            Run("validate");
            portal.Mode = "drop";
            Check("confirm offline fails", Run("confirm") == ExitCodes.Network);
            Check("confirm offline stores nothing", !File.Exists(Path.Combine(dir, "license.dat")));
            portal.Mode = "ok";
            Check("confirm retry works", Run("confirm") == ExitCodes.Ok);

            // The keyfile comes first, so a PC that cannot keep the license burns nothing.
            Reset();
            Run("validate");
            int burns = portal.Burns;
            Directory.CreateDirectory(Path.Combine(dir, "license.dat"));
            Check("unwritable PC reports failure", Run("confirm") == ExitCodes.NoLicense);
            Check("unwritable PC burns nothing", portal.Burns == burns);

            // The key is burned but the answer is lost: the keyfile waits, alerts stay off, retry finishes.
            Reset();
            Run("validate");
            burns = portal.Burns;
            portal.LoseNextConfirm = true;
            Check("lost answer reported", Run("confirm") == ExitCodes.Network);
            Check("lost answer keeps the keyfile", File.Exists(Path.Combine(dir, "license.pending")));
            Check("keyfile alone gives no alerts", Run("check") == ExitCodes.NoLicense);
            Check("lost answer retry works", Run("confirm") == ExitCodes.Ok);
            Check("lost answer burns once", portal.Burns == burns + 1);
            Check("alerts on after the signed answer", Run("check") == ExitCodes.Ok);
            Check("retry removes the keyfile", !File.Exists(Path.Combine(dir, "license.pending")));

            // The portal refuses: no keyfile stays behind.
            Reset();
            Run("validate");
            portal.ConfirmError = "limit_reached";
            Check("used key reported at confirm", Run("confirm") == ExitCodes.KeyUsed);
            Check("used key leaves no keyfile", !File.Exists(Path.Combine(dir, "license.pending")));
            portal.ConfirmError = null;
        }

        private static void TamperedFiles()
        {
            Install();
            string path = Path.Combine(dir, "license.dat");
            string good = File.ReadAllText(path);
            File.WriteAllText(path, good.Replace("2027", "2099"));
            Check("edited end date refused", Run("check") == ExitCodes.NoLicense);

            // A correctly signed file for this PC still fails if the seal does not match.
            File.WriteAllText(path, good.Replace("\"Seal\":\"", "\"Seal\":\"A"));
            Check("edited seal refused", Run("check") == ExitCodes.NoLicense);
            File.WriteAllText(path, good);
            string statePath = Path.Combine(dir, "state.dat");
            Check("sealed state accepted", Run("check") == ExitCodes.Ok);
            string goodState = File.ReadAllText(statePath);
            File.WriteAllText(statePath, goodState.Replace("LASTSEEN=2", "LASTSEEN=1"));
            Check("edited state refused", Run("check") == ExitCodes.NoLicense);
            File.WriteAllText(statePath, goodState);
            File.Delete(Path.Combine(dir, "seal.key"));
            Check("missing seal key refused", Run("check") == ExitCodes.NoLicense);
            Install();
            path = Path.Combine(dir, "license.dat");
            good = File.ReadAllText(path);

            var license = new JavaScriptSerializer().Deserialize<LicenseFile>(good);
            license.Components = Machine.Components().Select(c => c.Replace('a', 'b')).ToArray();
            license.Components[0] = new string('c', 64);
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(license));
            Check("edited machine hashes refused", Run("check") == ExitCodes.NoLicense);

            File.WriteAllText(path, "garbage");
            Check("garbage refused", Run("check") == ExitCodes.NoLicense);
            File.Delete(path);
            Check("missing refused", Run("check") == ExitCodes.NoLicense);
        }

        private static void MachineChanges()
        {
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_MACHINE", "m1,m2,m3,changed");
            Check("one part replaced still works", Run("check") == ExitCodes.Ok);
            Environment.SetEnvironmentVariable("TD_TEST_MACHINE", "x1,x2,x3,x4");
            Check("copied to another PC refused", Run("check") == ExitCodes.OtherMachine);
        }

        private static void ClockChecks()
        {
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2026-11-10T10:00:00");
            Check("normal date ok", Run("check") == ExitCodes.Ok);
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2026-11-09T10:00:00");
            Check("one day back tolerated", Run("check") == ExitCodes.Ok);
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2026-11-01T10:00:00");
            Check("clock moved back refused", Run("check") == ExitCodes.ClockBack);
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-06-01T10:00:00");
            Check("after end date expired", Run("check") == ExitCodes.Expired);
        }

        private static void Warnings()
        {
            Install();
            Check("no warning early", WarnAt("2027-03-01T10:00:00") == "0");
            Check("14 day warning", WarnAt("2027-04-20T10:00:00") == "14");
            Check("14 day warning once", WarnAt("2027-04-21T10:00:00") == "0");
            Check("7 day warning", WarnAt("2027-04-25T10:00:00") == "7");
            Check("1 day warning", WarnAt("2027-04-29T20:00:00") == "1");
            Check("1 day warning once", WarnAt("2027-04-30T01:00:00") == "0");
        }

        private static void Renewal()
        {
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-06-01T10:00:00");
            Check("expired before renewal", Run("check") == ExitCodes.Expired);
            Check("no renewal yet", Run("renew-check", "--force", "--quiet") == ExitCodes.NoChange);
            Check("repeat within 10 minutes skipped", Run("renew-check", "--quiet") == ExitCodes.NoChange);

            int calls = portal.Calls;
            Run("renew-check", "--quiet");
            Check("throttle made no call", portal.Calls == calls);

            portal.ErrorCode = "expired";
            Check("unsigned error reported", Run("renew-check", "--force", "--quiet") == ExitCodes.KeyExpired);
            Check("unsigned error changes nothing", Run("check") == ExitCodes.Expired);
            portal.ErrorCode = null;

            portal.Mode = "tamper";
            portal.ValidUntil = "2027-10-30T10:00:00+00:00";
            Check("forged renewal rejected", Run("renew-check", "--force", "--quiet") == ExitCodes.BadResponse);
            Check("forged renewal changes nothing", Run("check") == ExitCodes.Expired);
            portal.Mode = "ok";

            Check("real renewal found", Run("renew-check", "--force", "--quiet") == ExitCodes.Ok);
            Check("alerts back after renewal", Run("check") == ExitCodes.Ok);
            Check("status shows new date", File.ReadAllText(Path.Combine(dir, "license-status.json")).Contains("30-Oct-2027"));
        }

        private static void OneUseKeys()
        {
            Install();
            int calls = portal.ReleaseCalls;
            Check("nothing releases a key", Run("release") == ExitCodes.Usage && portal.ReleaseCalls == calls);
        }

        private static void ReviewFixes()
        {
            Reset();
            portal.Mode = "throttle";
            Check("Laravel throttle reads as rate limited", Run("validate") == ExitCodes.RateLimited);
            portal.Mode = "ok";

            // A saved validate answer must not count as a license: it burns nothing.
            Install();
            string path = Path.Combine(dir, "license.dat");
            var serializer = new JavaScriptSerializer();
            LicenseFile license = serializer.Deserialize<LicenseFile>(File.ReadAllText(path));
            string nonce = Machine.Nonce(license.Components, license.Random);
            string validatePayload = "{\"result\":\"ok\",\"nonce\":\"" + nonce + "\",\"confirm_token\":\"" + new string('t', 48) + "\",\"valid_until\":\"2099-01-01T00:00:00+00:00\"}";
            license.PayloadRaw = validatePayload;
            license.Signature = Convert.ToBase64String(Ed25519.Sign(Enumerable.Repeat((byte)7, 32).ToArray(), Encoding.UTF8.GetBytes(validatePayload)));
            new LicenseStore(dir).Save(license);
            Check("saved validate answer refused", Run("check") == ExitCodes.NoLicense);

            // Alerts start together: every one must pass, and a warning must show only once.
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-04-20T10:00:00");
            var results = new int[8];
            var outs = new string[8];
            var threads = Enumerable.Range(0, 8).Select(i => new Thread(() =>
            {
                outs[i] = Path.Combine(root, "out" + i + ".txt");
                results[i] = Run("check", "--out", outs[i]);
            })).ToList();
            threads.ForEach(t => t.Start());
            threads.ForEach(t => t.Join());
            Check("checks running together all pass", results.All(r => r == ExitCodes.Ok));
            Check("warning shows once when run together", outs.Count(o => File.ReadAllLines(o)[0] == "14") == 1);

            // Date set far ahead once, then corrected: reinstalling with the key clears it.
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-01-15T10:00:00");
            Run("check");
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2026-12-01T10:00:00");
            Check("wrong date then blocks", Run("check") == ExitCodes.ClockBack);
            Run("validate");
            Run("confirm");
            Check("reinstall clears the wrong date", Run("check") == ExitCodes.Ok);

            // Renewal task only near or after the end date.
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2026-12-01T10:00:00");
            Check("no renewal task far from the end", Run("can-renew") == ExitCodes.NoChange);
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-04-20T10:00:00");
            Check("renewal task near the end", Run("can-renew") == ExitCodes.Ok);
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-06-01T10:00:00");
            Check("renewal task after the end", Run("can-renew") == ExitCodes.Ok);
        }

        private static void KeyChanges()
        {
            const string newKey = "B1B2-C3D4-E5F6-0718-9A0B-1C2D-3E4F-5062";
            string path;

            // Refused before any network call.
            Install();
            path = Path.Combine(dir, "license.dat");
            string before = File.ReadAllText(path);
            int calls = portal.Calls;
            Check("change: bad format refused", new KeyChange(dir).Preview("not-a-key") == ExitCodes.InvalidKey);
            Check("change: empty refused", new KeyChange(dir).Preview("") == ExitCodes.InvalidKey);
            Check("change: same key refused", new KeyChange(dir).Preview(" " + MockPortal.GoodKey.ToLowerInvariant() + " ") == ExitCodes.SameKey);
            Check("change: no network for refused keys", portal.Calls == calls);

            // Preview burns nothing and changes nothing.
            int burns = portal.Burns;
            var change = new KeyChange(dir);
            portal.ValidUntil = "2028-01-31T10:00:00+00:00";
            Check("change: preview ok", change.Preview(newKey) == ExitCodes.Ok);
            Check("change: preview burns nothing", portal.Burns == burns);
            Check("change: preview keeps the license", File.ReadAllText(path) == before);
            Check("change: preview shows both dates", change.NewEnd.Year == 2028 && change.CurrentEnd.Value.Year == 2027);
            Check("change: only last 4 shown", change.NewKeyEnding == "5062");

            // Apply swaps the key and asks the portal to release nothing.
            calls = portal.ReleaseCalls;
            Check("change: apply ok", change.Apply() == ExitCodes.Ok);
            Check("change: nothing released", portal.ReleaseCalls == calls);
            Check("change: new key burned", portal.Burns == burns + 1);
            string after = File.ReadAllText(path);
            var serializer = new JavaScriptSerializer();
            Check("change: new key stored", LicenseStore.Unprotect(serializer.Deserialize<LicenseFile>(after).KeyProtected) == newKey);
            Check("change: neither key in plain text", !after.Contains(newKey) && !after.Contains(MockPortal.GoodKey));
            Check("change: alerts pass", Run("check") == ExitCodes.Ok);
            Check("change: status shows new date", File.ReadAllText(Path.Combine(dir, "license-status.json")).Contains("2028"));
            Check("change: no key in other files", Directory.GetFiles(dir).All(f => !File.ReadAllText(f).Contains(newKey) && !File.ReadAllText(f).Contains(MockPortal.GoodKey)));
            Check("change: apply cannot run twice", change.Apply() == ExitCodes.Usage);

            // Earlier warnings do not carry over to the new license.
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-04-20T10:00:00");
            Run("check");
            portal.ValidUntil = "2027-04-25T10:00:00+00:00";
            change = new KeyChange(dir);
            change.Preview(newKey);
            change.Apply();
            Run("check");
            Check("change: warnings start again", File.ReadAllLines(outFile)[0] == "7");

            // Failures leave the old license exactly as it was.
            foreach (string error in new[] { "invalid_key", "expired", "revoked", "subscription_suspended", "limit_reached", "rate_limited" })
            {
                Install();
                path = Path.Combine(dir, "license.dat");
                before = File.ReadAllText(path);
                portal.ErrorCode = error;
                Check("change: " + error + " reported", new KeyChange(dir).Preview(newKey) != ExitCodes.Ok);
                Check("change: " + error + " keeps license", File.ReadAllText(path) == before);
            }
            foreach (string mode in new[] { "tamper", "wrong-key", "wrong-nonce", "unsigned", "html", "redirect" })
            {
                Install();
                path = Path.Combine(dir, "license.dat");
                before = File.ReadAllText(path);
                portal.Mode = mode;
                Check("change: " + mode + " refused", new KeyChange(dir).Preview(newKey) == ExitCodes.BadResponse);
                Check("change: " + mode + " keeps license", File.ReadAllText(path) == before);
            }
            Install();
            path = Path.Combine(dir, "license.dat");
            before = File.ReadAllText(path);
            portal.Mode = "drop";
            Check("change: offline reported", new KeyChange(dir).Preview(newKey) == ExitCodes.Network);
            portal.Mode = "ok";

            // A forged confirm answer after a good preview changes nothing.
            change = new KeyChange(dir);
            change.Preview(newKey);
            portal.Mode = "tamper";
            Check("change: forged confirm refused", change.Apply() == ExitCodes.BadResponse);
            Check("change: forged confirm keeps license", File.ReadAllText(path) == before);
            portal.Mode = "ok";

            change = new KeyChange(dir);
            change.Preview(newKey);
            portal.ConfirmError = "limit_reached";
            Check("change: used key reported", change.Apply() == ExitCodes.KeyUsed);
            Check("change: used key keeps license", File.ReadAllText(path) == before);
            portal.ConfirmError = null;

            // An old token is replaced once.
            change = new KeyChange(dir);
            change.Preview(newKey);
            portal.ConfirmFailures = 1;
            Check("change: old token retried", change.Apply() == ExitCodes.Ok);

            // If this PC cannot save the license, the new key is not burned and the old license stays.
            Install();
            path = Path.Combine(dir, "license.dat");
            before = File.ReadAllText(path);
            change = new KeyChange(dir);
            change.Preview(newKey);
            burns = portal.Burns;
            File.SetAttributes(path, FileAttributes.ReadOnly);
            Check("change: failed save reported", change.Apply() == ExitCodes.NoLicense);
            File.SetAttributes(path, FileAttributes.Normal);
            Check("change: failed save keeps license", File.ReadAllText(path) == before);
            Check("change: failed save burns nothing", portal.Burns == burns);
            Check("change: alerts still pass", Run("check") == ExitCodes.Ok);

            // A lost answer: the old license stays until the same token finishes the change.
            Install();
            path = Path.Combine(dir, "license.dat");
            before = File.ReadAllText(path);
            change = new KeyChange(dir);
            change.Preview(newKey);
            burns = portal.Burns;
            portal.LoseNextConfirm = true;
            Check("change: lost answer reported", change.Apply() == ExitCodes.Network);
            Check("change: lost answer keeps license", File.ReadAllText(path) == before);
            calls = portal.Calls;
            Check("change: retry skips validate", change.Preview(newKey) == ExitCodes.Ok && portal.Calls == calls);
            Check("change: retry finishes", change.Apply() == ExitCodes.Ok);
            Check("change: lost answer burns once", portal.Burns == burns + 1);
            Check("change: retry leaves no keyfile", !File.Exists(Path.Combine(dir, "license.pending")));

            // An ended license and a missing one can both move to a new key.
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-06-01T10:00:00");
            Check("change: license has ended", Run("check") == ExitCodes.Expired);
            portal.ValidUntil = "2028-06-01T10:00:00+00:00";
            change = new KeyChange(dir);
            Check("change: ended license can preview", change.Preview(newKey) == ExitCodes.Ok);
            Check("change: ended license can apply", change.Apply() == ExitCodes.Ok);
            Check("change: alerts back on", Run("check") == ExitCodes.Ok);
            Reset();
            change = new KeyChange(dir);
            Check("change: no license can preview", change.Preview(newKey) == ExitCodes.Ok);
            Check("change: no license can apply", change.Apply() == ExitCodes.Ok);
            Check("change: stale answer dated in the past refused", StaleDateRefused(newKey));

            // Another Windows account cannot decrypt the key, so it cannot replace it.
            foreach (string stored in new[] { "AAAA", "not base64!", null })
            {
                Install();
                path = Path.Combine(dir, "license.dat");
                LicenseFile license = serializer.Deserialize<LicenseFile>(File.ReadAllText(path));
                license.KeyProtected = stored;
                new LicenseStore(dir).Save(license);
                calls = portal.Calls;
                Check("change: other account refused", new KeyChange(dir).Preview(newKey) == ExitCodes.OtherAccount);
                Check("change: other account makes no call", portal.Calls == calls);
            }

            // Wrong keys: 5 tries, then an hour, and the portal is not called meanwhile.
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2026-12-01T10:00:00");
            portal.ErrorCode = "invalid_key";
            for (int i = 0; i < 5; i++) new KeyChange(dir).Preview(newKey);
            calls = portal.Calls;
            Check("change: locked after 5 wrong keys", new KeyChange(dir).Preview(newKey) == ExitCodes.TooManyTries);
            Check("change: locked makes no call", portal.Calls == calls);
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2026-12-01T11:01:00");
            portal.ErrorCode = null;
            Check("change: tries again after an hour", new KeyChange(dir).Preview(newKey) == ExitCodes.Ok);
            Check("change: good key clears the count", !File.ReadAllText(Path.Combine(dir, "state.dat")).Contains("KEY_FAILS"));

            // A renewal check already running must not put the old key back.
            Install();
            Environment.SetEnvironmentVariable("TD_TEST_NOW", "2027-06-01T10:00:00");
            path = Path.Combine(dir, "license.dat");
            LicenseFile swapped = serializer.Deserialize<LicenseFile>(File.ReadAllText(path));
            swapped.KeyProtected = LicenseStore.Protect(newKey);
            portal.ValidUntil = "2028-06-01T10:00:00+00:00";
            string swappedText = null;
            string swapDir = dir;
            portal.OnHeartbeat = () =>
            {
                new LicenseStore(swapDir).Save(swapped);
                swappedText = File.ReadAllText(path);
            };
            Check("change: renewal check backs off", Run("renew-check", "--force", "--quiet") == ExitCodes.NoChange);
            portal.OnHeartbeat = null;
            Check("change: renewal check keeps new key", File.ReadAllText(path) == swappedText);
        }

        private static bool StaleDateRefused(string key)
        {
            Reset();
            portal.ValidUntil = "2020-01-01T10:00:00+00:00";
            return new KeyChange(dir).Preview(key) == ExitCodes.BadResponse;
        }

        private static string WarnAt(string now)
        {
            Environment.SetEnvironmentVariable("TD_TEST_NOW", now);
            Run("check");
            return File.ReadAllLines(outFile)[0];
        }

        private static void Install()
        {
            Reset();
            Run("validate");
            Run("confirm");
        }

        private static void Reset()
        {
            portal.Mode = "ok";
            portal.ErrorCode = null;
            portal.ConfirmFailures = 0;
            portal.ConfirmError = null;
            portal.LoseNextConfirm = false;
            portal.ValidUntil = "2027-04-30T10:00:00+00:00";
            Environment.SetEnvironmentVariable("TD_TEST_MACHINE", "m1,m2,m3,m4");
            Environment.SetEnvironmentVariable("TD_TEST_NOW", null);
            dir = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(keyFile, MockPortal.GoodKey);
        }

        private static int Run(string command, params string[] extra)
        {
            var args = new List<string> { command, "--install-dir", dir, "--key-file", keyFile, "--state", stateFile, "--out", outFile };
            args.AddRange(extra);
            return Program.Guarded(args.ToArray());
        }

        private static void Check(string name, bool ok)
        {
            if (ok) { passed++; return; }
            failed++;
            Console.WriteLine("FAILED: " + name);
        }
    }
}

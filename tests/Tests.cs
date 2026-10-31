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
        public int SeatsTaken;

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
            if (Mode == "redirect") { status = "302 Found"; json = "{}"; return; }
            if (Mode == "html") { json = "<html>captive wifi page</html>"; return; }
            if (ErrorCode != null) { status = ErrorCode == "invalid_key" ? "404 Not Found" : ErrorCode == "rate_limited" ? "429 Too Many" : "403 Forbidden"; json = "{\"error\":\"" + ErrorCode + "\"}"; return; }
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
                SeatsTaken--;
                payload = "{\"result\":\"ok\",\"nonce\":\"" + nonce + "\"}";
            }
            else
            {
                if (path.EndsWith("/confirm")) SeatsTaken++;
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
                ReleaseSeat();
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
            int seats = portal.SeatsTaken;
            Check("validate ok", Run("validate") == ExitCodes.Ok);
            Check("validate takes no seat", portal.SeatsTaken == seats);
            Check("no license before confirm", Run("check") == ExitCodes.NoLicense);
            Check("confirm ok", Run("confirm") == ExitCodes.Ok);
            Check("confirm takes a seat", portal.SeatsTaken == seats + 1);
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
                { "subscription_suspended", ExitCodes.Suspended }, { "limit_reached", ExitCodes.LimitReached }, { "rate_limited", ExitCodes.RateLimited }
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
        }

        private static void TamperedFiles()
        {
            Install();
            string path = Path.Combine(dir, "license.dat");
            string good = File.ReadAllText(path);
            File.WriteAllText(path, good.Replace("2027", "2099"));
            Check("edited end date refused", Run("check") == ExitCodes.NoLicense);

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

        private static void ReleaseSeat()
        {
            Install();
            int seats = portal.SeatsTaken;
            Check("release ok", Run("release") == ExitCodes.Ok);
            Check("release frees the seat", portal.SeatsTaken == seats - 1);
            portal.Mode = "drop";
            Check("release offline reported", Run("release") == ExitCodes.Network);
            portal.Mode = "ok";
            File.Delete(Path.Combine(dir, "license.dat"));
            Check("release with no license is fine", Run("release") == ExitCodes.Ok);
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

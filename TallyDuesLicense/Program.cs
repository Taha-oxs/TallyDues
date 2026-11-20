// TallyDuesLicense.exe: the single place for license logic, used by Setup and the alert scripts.
// It shows no window. Results are exit codes (see ExitCodes), plus --out text for "check".
// The key is read from a file and is never written to a log or a command line.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace TallyDues.License
{
    internal static class ExitCodes
    {
        public const int Ok = 0;
        public const int Usage = 2;
        public const int Expired = 10;
        public const int NoLicense = 11;
        public const int ClockBack = 12;
        public const int OtherMachine = 13;
        public const int InvalidKey = 20;
        public const int KeyExpired = 21;
        public const int Revoked = 22;
        public const int Suspended = 23;
        public const int KeyUsed = 24;
        public const int RateLimited = 25;
        public const int Network = 30;
        public const int BadResponse = 31;
        public const int NotConfigured = 32;
        public const int NoChange = 40;
        public const int SameKey = 41;
        public const int TooManyTries = 42;
        public const int OtherAccount = 43;
        public const int ReleaseUnfinished = 44;
        public const int Released = 45;
    }

    internal static class Program
    {
        private const string RenewTask = "TallyDues-RenewCheck";
        private static readonly TimeSpan RetryGap = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan BeatGap = TimeSpan.FromDays(7);
        private static readonly TimeSpan BeatRetryGap = TimeSpan.FromDays(1);

        private static int Main(string[] args)
        {
            return Guarded(args);
        }

        internal static int Guarded(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (PortalException e)
            {
                return e.ExitCode;
            }
            catch (Exception)
            {
                return ExitCodes.NoLicense;
            }
        }

        internal static int Run(string[] args)
        {
            if (args.Length == 0) return ExitCodes.Usage;
            var options = ReadOptions(args);
            string dir = Option(options, "install-dir", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TallyDues"));
            switch (args[0])
            {
                case "validate": return Validate(options);
                case "confirm": return Confirm(options, dir);
                case "check": return Check(options, dir);
                case "renew-check": return RenewCheck(options, dir);
                case "heartbeat": return Heartbeat(dir);
                case "open-renewal": return OpenRenewal();
                case "can-renew": return CanRenew(dir);
                case "release": return LicenseRelease.Run(dir);
#if !TEST_BUILD
                // Takes no key option on purpose: the key can only be typed into the window.
                case "change-key": return KeyChangeWindow.ShowWindow(dir);
                case "release-window": return ReleaseWindow.ShowWindow(dir);
#endif
                default: return ExitCodes.Usage;
            }
        }

        // Cheque and invoice alerts start together, so file updates take turns across processes.
        // If the wait runs out the work goes ahead, because a late alert is worse than a lost warning.
        internal static T WithLock<T>(Func<T> action)
        {
            using (var mutex = new Mutex(false, @"Local\TallyDues-License"))
            {
                bool owned = false;
                try
                {
                    try
                    {
                        owned = mutex.WaitOne(TimeSpan.FromSeconds(10));
                    }
                    catch (AbandonedMutexException)
                    {
                        owned = true;
                    }
                    return action();
                }
                finally
                {
                    if (owned) mutex.ReleaseMutex();
                }
            }
        }

        // Read-only check before installing. Keeps the confirm token in a temp state file.
        private static int Validate(Dictionary<string, string> options)
        {
            string key = ReadKey(options);
            if (key == null) return ExitCodes.InvalidKey;
            string[] machine = Machine.Components();
            string random = NewRandom();
            string nonce = Machine.Nonce(machine, random);
            SignedAnswer answer = Portal.Post("/api/v1/tally/validate", Request("key", key, machine, nonce), nonce);
            string token = Portal.AsString(answer.Payload, "confirm_token");
            if (token.Length != 48) return ExitCodes.BadResponse;
            File.WriteAllText(options["state"], token, Encoding.ASCII);
            return ExitCodes.Ok;
        }

        // After a successful install: writes the keyfile, tells the portal, which takes the seat,
        // then stores the signed license. Alerts stay off until the signed answer is stored.
        private static int Confirm(Dictionary<string, string> options, string dir)
        {
            string key = ReadKey(options);
            if (key == null) return ExitCodes.InvalidKey;
            string[] machine = Machine.Components();
            string token = File.ReadAllText(options["state"], Encoding.ASCII).Trim();
            string random = NewRandom();
            string nonce = Machine.Nonce(machine, random);
            var store = new LicenseStore(dir);
            if (!store.WritePending(key, machine, random)) return ExitCodes.NoLicense;
            SignedAnswer answer;
            try
            {
                try
                {
                    answer = Portal.Post("/api/v1/tally/confirm", Request("confirm_token", token, machine, nonce), nonce);
                }
                catch (PortalException e)
                {
                    // The token lasts 30 minutes and works once. An old token reports invalid_key.
                    if (e.ExitCode != ExitCodes.InvalidKey) throw;
                    int validated = Validate(options);
                    if (validated != ExitCodes.Ok)
                    {
                        store.RemovePending();
                        return validated;
                    }
                    token = File.ReadAllText(options["state"], Encoding.ASCII).Trim();
                    answer = Portal.Post("/api/v1/tally/confirm", Request("confirm_token", token, machine, nonce), nonce);
                }
            }
            catch (PortalException e)
            {
                // After a lost connection the portal may have taken the seat, so the retry needs the keyfile.
                if (e.ExitCode != ExitCodes.Network) store.RemovePending();
                throw;
            }
            return StoreConfirmed(store, key, machine, random, answer);
        }

        // Saves a confirmed license. If it cannot be saved, any license already there stays as it was.
        // The seat is taken by now, which is why WritePending tests the folder first.
        internal static int StoreConfirmed(LicenseStore store, string key, string[] machine, string random, SignedAnswer answer)
        {
            var license = new LicenseFile
            {
                KeyProtected = LicenseStore.Protect(key),
                Components = machine,
                Random = random,
                PayloadRaw = answer.PayloadRaw,
                Signature = answer.Signature
            };
            if (!license.IsAuthentic())
            {
                store.RemovePending();
                return ExitCodes.BadResponse;
            }
            try
            {
                WithLock<object>(() =>
                {
                    store.Save(license);
                    return null;
                });
            }
            catch (IOException)
            {
                return ExitCodes.NoLicense;
            }
            catch (UnauthorizedAccessException)
            {
                return ExitCodes.NoLicense;
            }
            // A new license starts clean, so a wrong date seen earlier cannot block it.
            WithLock<object>(() =>
            {
                Dictionary<string, string> state = store.LoadState();
                state.Remove("LASTSEEN");
                state.Remove("WARN_FOR");
                state.Remove("WARN_LEVEL");
                state.Remove("LAST_TRY");
                state.Remove("KEY_FAILS");
                state.Remove("KEY_FAIL_AT");
                state.Remove("WITHDRAWN");
                store.SaveState(state);
                store.WriteStatus("License valid until " + ShowDate(license.ValidUntil()), license.Licensee());
                return null;
            });
            return ExitCodes.Ok;
        }

        // Only a license that has ended or is about to end may start the renewal task, since
        // any web page can open a tallydues:// link.
        private static int CanRenew(string dir)
        {
            LicenseFile license = new LicenseStore(dir).Load();
            if (license == null || !license.IsAuthentic()) return ExitCodes.NoLicense;
            return (license.ValidUntil() - new DateTimeOffset(Now())).TotalDays <= 14 ? ExitCodes.Ok : ExitCodes.NoChange;
        }

        // Local gate, run before every alert. Makes no network call.
        private static int Check(Dictionary<string, string> options, string dir)
        {
            var store = new LicenseStore(dir);
            string warnLevel = "0";
            string message = "";
            int result = WithLock(() => Evaluate(store, out warnLevel, out message));
            string outPath = Option(options, "out", null);
            if (outPath != null) File.WriteAllText(outPath, warnLevel + "\r\n" + message, Encoding.ASCII);
            return result;
        }

        private static int Evaluate(LicenseStore store, out string warnLevel, out string message)
        {
            warnLevel = "0";
            LicenseFile license = store.Load();
            if (license == null)
            {
                // Only a signed portal answer writes WITHDRAWN, and the state seal must still match.
                string withdrawn;
                Dictionary<string, string> left = store.LoadState();
                if (!store.StateTampered && left.TryGetValue("WITHDRAWN", out withdrawn) && (withdrawn == "released" || withdrawn == "revoked"))
                {
                    message = withdrawn == "released"
                        ? "Your TallyDues license was released from this PC on the OpenXpert portal, so alerts have stopped. To use TallyDues here again, enter your license key with TallyDues Change License Key."
                        : "Your TallyDues license key is no longer active, so alerts have stopped. Please contact OpenXpert Solutions.";
                    store.WriteStatus(LicenseStore.WithdrawnStatus(withdrawn));
                    return ExitCodes.NoLicense;
                }
            }
            if (license == null || !license.IsAuthentic())
            {
                message = "TallyDues has no valid license on this PC. Enter your license key with TallyDues Change License Key. If that key is active on another PC, release it there first, or contact OpenXpert Solutions.";
                store.WriteStatus("No valid license");
                return ExitCodes.NoLicense;
            }
            if (!Machine.IsSameMachine(license.Components, Machine.Components()))
            {
                message = "This TallyDues license belongs to a different PC. Release it on that PC, then enter your key here with TallyDues Change License Key. If you cannot reach that PC, please contact OpenXpert Solutions.";
                store.WriteStatus("License belongs to a different PC");
                return ExitCodes.OtherMachine;
            }
            Dictionary<string, string> state = store.LoadState();
            if (store.StateTampered)
            {
                message = "The TallyDues license records on this PC were changed, so alerts have stopped. Please contact OpenXpert Solutions.";
                store.WriteStatus("License records were changed");
                return ExitCodes.NoLicense;
            }
            DateTime today = Now().Date;
            DateTime lastSeen;
            string seen;
            // One day of tolerance, so that a time zone change does not block anyone.
            if (state.TryGetValue("LASTSEEN", out seen)
                && DateTime.TryParseExact(seen, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out lastSeen)
                && today < lastSeen.AddDays(-1))
            {
                message = "The Windows date on this PC looks wrong, so TallyDues has paused its alerts. Please correct the date and time.";
                return ExitCodes.ClockBack;
            }
            if (!state.ContainsKey("LASTSEEN") || string.CompareOrdinal(today.ToString("yyyyMMdd", CultureInfo.InvariantCulture), state["LASTSEEN"]) > 0)
            {
                state["LASTSEEN"] = today.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            }
            DateTimeOffset end = license.ValidUntil();
            string endText = ShowDate(end);
            if (new DateTimeOffset(Now()) > end)
            {
                store.SaveState(state);
                store.WriteStatus("License ended on " + endText, license.Licensee());
                message = "Your TallyDues license ended on " + endText + ", so alerts have stopped. Click Renew to renew it. If you have a new license key, use TallyDues Change License Key on the Gateway of Tally.";
                return ExitCodes.Expired;
            }
            double days = (end - new DateTimeOffset(Now())).TotalDays;
            warnLevel = NextWarning(state, end, days);
            store.SaveState(state);
            store.WriteStatus("License valid until " + endText, license.Licensee());
            message = warnLevel == "0"
                ? "OK"
                : "Your TallyDues license ends on " + endText + ". Click Renew to keep your alerts.";
            return ExitCodes.Ok;
        }

        // Each warning (14, 7 and 1 days before the end) shows once per end date.
        private static string NextWarning(Dictionary<string, string> state, DateTimeOffset end, double days)
        {
            string level = days <= 1 ? "1" : days <= 7 ? "7" : days <= 14 ? "14" : "0";
            if (level == "0") return "0";
            string endKey = end.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string shownFor, shownLevel;
            if (state.TryGetValue("WARN_FOR", out shownFor) && shownFor == endKey
                && state.TryGetValue("WARN_LEVEL", out shownLevel)
                && int.Parse(shownLevel, CultureInfo.InvariantCulture) <= int.Parse(level, CultureInfo.InvariantCulture))
            {
                return "0";
            }
            state["WARN_FOR"] = endKey;
            state["WARN_LEVEL"] = level;
            return level;
        }

        // Asks the portal for a later end date. Only a signed, later date changes anything.
        private static int RenewCheck(Dictionary<string, string> options, string dir)
        {
            var store = new LicenseStore(dir);
            LicenseFile license = store.Load();
            if (license == null || !license.IsAuthentic()) return ExitCodes.NoLicense;
            Dictionary<string, string> state = store.LoadState();
            // Saving would seal changed records again, so they are left for Check to report.
            if (store.StateTampered) return ExitCodes.NoLicense;
            if (!options.ContainsKey("force") && Recent(state, "LAST_TRY", RetryGap)) return ExitCodes.NoChange;
            if (!Stamp(store, "LAST_TRY")) return ExitCodes.NoLicense;

            string key = LicenseStore.Unprotect(license.KeyProtected);
            string[] machine = Machine.Components();
            string random = NewRandom();
            string nonce = Machine.Nonce(machine, random);
            SignedAnswer answer = Portal.Post("/api/v1/tally/heartbeat", Request("key", key, machine, nonce), nonce, "ok", "released", "revoked");
            string result = Portal.AsString(answer.Payload, "result");
            if (result != "ok") return Withdraw(store, license, result == "revoked");
            var renewed = new LicenseFile
            {
                KeyProtected = license.KeyProtected,
                Components = machine,
                Random = random,
                PayloadRaw = answer.PayloadRaw,
                Signature = answer.Signature
            };
            if (!renewed.IsAuthentic()) return ExitCodes.BadResponse;
            if (renewed.ValidUntil() <= license.ValidUntil()) return ExitCodes.NoChange;

            string endText = ShowDate(renewed.ValidUntil());
            bool replaced = false;
            WithLock<object>(() =>
            {
                // The key may have been changed during the portal call. Never put the old one back.
                LicenseFile latest = store.Load();
                if (latest == null || latest.KeyProtected != license.KeyProtected)
                {
                    replaced = true;
                    return null;
                }
                store.Save(renewed);
                ClearWarnings(store);
                store.WriteStatus("License valid until " + endText, renewed.Licensee());
                return null;
            });
            if (replaced) return ExitCodes.NoChange;
            RemoveRenewTask();
            if (!options.ContainsKey("quiet")) ShowRenewed(dir, endText);
            return ExitCodes.Ok;
        }

        // A signed "released" or "revoked" answer: the seat is no longer this PC's, so the license goes,
        // as after Release License. Unsigned errors never get here.
        private static int Withdraw(LicenseStore store, LicenseFile license, bool revoked)
        {
            bool removed = WithLock(() =>
            {
                // A new key that arrived during the portal call keeps its license.
                LicenseFile latest = store.Load();
                if (latest == null || latest.KeyProtected != license.KeyProtected) return false;
                store.RemoveLicense(revoked ? "revoked" : "released");
                return true;
            });
            if (!removed) return ExitCodes.NoChange;
            RemoveRenewTask();
            return revoked ? ExitCodes.Revoked : ExitCodes.Released;
        }

        // Started in the background by each alert run, and calls the portal about once a week.
        // It brings in renewals made early and a release or revocation on the portal.
        // Being offline changes nothing: alerts keep working until the stored end date.
        private static int Heartbeat(string dir)
        {
            var store = new LicenseStore(dir);
            LicenseFile license = store.Load();
            if (license == null || !license.IsAuthentic()) return ExitCodes.NoLicense;
            Dictionary<string, string> state = store.LoadState();
            if (store.StateTampered) return ExitCodes.NoLicense;
            // After a failed call, the next try waits a day rather than a week.
            if (Recent(state, "LAST_BEAT", BeatGap) || Recent(state, "BEAT_TRY", BeatRetryGap)) return ExitCodes.NoChange;
            if (!Stamp(store, "BEAT_TRY")) return ExitCodes.NoLicense;

            int result = RenewCheck(new Dictionary<string, string> { { "force", "" }, { "quiet", "" } }, dir);
            if (result == ExitCodes.Ok || result == ExitCodes.NoChange) Stamp(store, "LAST_BEAT");
            return result;
        }

        // True when the time stored under name is less than gap ago. A time in the future counts as old,
        // so a clock set forward and then back cannot stop the calls for good.
        private static bool Recent(Dictionary<string, string> state, string name, TimeSpan gap)
        {
            string text;
            long ticks;
            if (!state.TryGetValue(name, out text) || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ticks)) return false;
            if (ticks > DateTime.MaxValue.Ticks) return false;
            TimeSpan age = Now() - new DateTime(ticks);
            return age >= TimeSpan.Zero && age < gap;
        }

        // Saves the time now under name, unless the records were changed or the license has gone.
        private static bool Stamp(LicenseStore store, string name)
        {
            return WithLock(() =>
            {
                if (store.Load() == null) return false;
                Dictionary<string, string> latest = store.LoadState();
                if (store.StateTampered) return false;
                latest[name] = Now().Ticks.ToString(CultureInfo.InvariantCulture);
                store.SaveState(latest);
                return true;
            });
        }

        // The address is fixed at build time and never taken from a link.
        private static int OpenRenewal()
        {
            if (!Config.IsConfigured) return ExitCodes.NotConfigured;
            Process.Start(new ProcessStartInfo(Config.PortalUrl.TrimEnd('/') + "/portal") { UseShellExecute = true });
            return ExitCodes.Ok;
        }

        private static void ShowRenewed(string dir, string endText)
        {
            string bridge = Path.Combine(dir, "TDV2Toast.vbs");
            if (!File.Exists(bridge)) return;
            string arguments = "//B //Nologo \"" + bridge + "\" TallyDues general Your TallyDues license is renewed until " + endText + ". Alerts are back on.";
            Process.Start(new ProcessStartInfo("wscript.exe", arguments) { UseShellExecute = false, CreateNoWindow = true });
        }

        internal static void RemoveRenewTask()
        {
#if !TEST_BUILD
            try
            {
                using (Process process = Process.Start(new ProcessStartInfo("schtasks.exe", "/Delete /TN " + RenewTask + " /F") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                {
                    process.WaitForExit(10000);
                }
            }
            catch (Exception)
            {
                // The task stops by itself after 14 days.
            }
#endif
        }

        private static void ClearWarnings(LicenseStore store)
        {
            Dictionary<string, string> state = store.LoadState();
            state.Remove("WARN_FOR");
            state.Remove("WARN_LEVEL");
            store.SaveState(state);
        }

        internal static Dictionary<string, object> Request(string credentialName, string credential, string[] machine, string nonce)
        {
            return new Dictionary<string, object>
            {
                { credentialName, credential },
                { "machine", machine },
                { "nonce", nonce },
                { "installer_version", Config.Version }
            };
        }

        private static string ReadKey(Dictionary<string, string> options)
        {
            return NormalizeKey(File.ReadAllText(options["key-file"], Encoding.ASCII));
        }

        internal static string NormalizeKey(string text)
        {
            string key = (text ?? "").Trim().ToUpperInvariant();
            return Regex.IsMatch(key, "^[0-9A-F]{4}(-[0-9A-F]{4}){7}$") ? key : null;
        }

        internal static string NewRandom()
        {
            byte[] bytes = new byte[32];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            return string.Concat(bytes.Select(b => b.ToString("x2")));
        }

        private static string ShowDate(DateTimeOffset date)
        {
            return date.LocalDateTime.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
        }

        internal static DateTime Now()
        {
#if TEST_BUILD
            string fake = Environment.GetEnvironmentVariable("TD_TEST_NOW");
            if (!string.IsNullOrEmpty(fake)) return DateTime.Parse(fake, CultureInfo.InvariantCulture);
#endif
            return DateTime.Now;
        }

        private static Dictionary<string, string> ReadOptions(string[] args)
        {
            var options = new Dictionary<string, string>();
            for (int i = 1; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--")) continue;
                string name = args[i].Substring(2);
                bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--");
                options[name] = hasValue ? args[++i] : "";
            }
            return options;
        }

        private static string Option(Dictionary<string, string> options, string name, string fallback)
        {
            string value;
            return options.TryGetValue(name, out value) && value.Length > 0 ? value : fallback;
        }
    }
}

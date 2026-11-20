// Changes the license key on a PC that already has TallyDues. The window in KeyChangeWindow.cs
// is the only caller, so the key arrives from what the user typed, never from a file or a link.
// Order matters: the old license stays in place until the new one is signed and saved.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace TallyDues.License
{
    internal sealed class KeyChange
    {
        private const int MaxTries = 5;
        // With 5 tries, one PC stays under the portal's limit of 10 invalid keys an hour per address.
        private static readonly TimeSpan LockTime = TimeSpan.FromHours(1);

        private readonly LicenseStore store;
        private string newKey;
        private string token;
        private string[] machine;
        // The connection dropped during Apply, so the portal may have taken the seat. Only the same token may finish it.
        private bool retryPending;

        public DateTimeOffset NewEnd { get; private set; }
        public DateTimeOffset? CurrentEnd { get; private set; }

        public KeyChange(string dir)
        {
            store = new LicenseStore(dir);
        }

        public string NewKeyEnding
        {
            get { return newKey == null ? "" : newKey.Substring(newKey.Length - 4); }
        }

        // Checks the new key with the portal. Takes no seat and changes no license.
        public int Preview(string typed)
        {
            try
            {
                string key = Program.NormalizeKey(typed);
                if (retryPending && key == newKey) return ExitCodes.Ok;
                retryPending = false;
                newKey = null;
                if (key == null) return ExitCodes.InvalidKey;
                string oldKey;
                int current = ReadCurrent(out oldKey);
                if (current != ExitCodes.Ok) return current;
                if (key == oldKey) return ExitCodes.SameKey;
                if (IsLockedOut()) return ExitCodes.TooManyTries;

                machine = Machine.Components();
                try
                {
                    RequestToken(key);
                }
                catch (PortalException e)
                {
                    if (e.ExitCode == ExitCodes.InvalidKey) CountFailure();
                    return e.ExitCode;
                }
                ClearFailures();
                newKey = key;
                return ExitCodes.Ok;
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

        // Writes the keyfile, tells the portal, which takes a seat for the new key, then saves the new license.
        // No release is sent, so the old key keeps this PC's seat. The confirm step tells the user so.
        public int Apply()
        {
            if (newKey == null) return ExitCodes.Usage;
            try
            {
                retryPending = false;
                // Read again: a renewal or Setup may have changed the license since Preview.
                string oldKey;
                int current = ReadCurrent(out oldKey);
                if (current != ExitCodes.Ok) return current;
                if (newKey == oldKey) return ExitCodes.SameKey;

                string random = Program.NewRandom();
                string nonce = Machine.Nonce(machine, random);
                if (!store.WritePending(newKey, machine, random)) return ExitCodes.NoLicense;
                SignedAnswer answer;
                try
                {
                    try
                    {
                        answer = Portal.Post("/api/v1/tally/confirm", Program.Request("confirm_token", token, machine, nonce), nonce);
                    }
                    catch (PortalException e)
                    {
                        // The token lasts 30 minutes and works once. An old token reports invalid_key.
                        if (e.ExitCode != ExitCodes.InvalidKey) throw;
                        RequestToken(newKey);
                        answer = Portal.Post("/api/v1/tally/confirm", Program.Request("confirm_token", token, machine, nonce), nonce);
                    }
                }
                catch (PortalException e)
                {
                    retryPending = e.ExitCode == ExitCodes.Network;
                    if (!retryPending) store.RemovePending();
                    throw;
                }

                int stored = Program.StoreConfirmed(store, newKey, machine, random, answer);
                if (stored != ExitCodes.Ok) return stored;

                Program.RemoveRenewTask();
                NewEnd = DateTimeOffset.Parse(Portal.AsString(answer.Payload, "valid_until"), CultureInfo.InvariantCulture);
                newKey = null;
                token = null;
                return ExitCodes.Ok;
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

        public static string Message(int code)
        {
            const string unchanged = " Your current license has not changed.";
            switch (code)
            {
                case ExitCodes.InvalidKey: return "That license key was not accepted. Check it on the OpenXpert portal under My licenses." + unchanged;
                case ExitCodes.SameKey: return "This license key is already in use on this PC.";
                case ExitCodes.TooManyTries: return "Too many wrong keys were entered. Please try again in an hour." + unchanged;
                case ExitCodes.OtherAccount: return "This license was set up from another Windows account, or its file is damaged. Sign in to that account, or run Setup again and enter your license key." + unchanged;
                case ExitCodes.KeyExpired: return "That license key has ended. Renew it on the OpenXpert portal." + unchanged;
                case ExitCodes.Revoked: return "That license key has been revoked. Please contact OpenXpert Solutions." + unchanged;
                case ExitCodes.Suspended: return "The subscription for that license key is suspended. Please contact OpenXpert Solutions." + unchanged;
                case ExitCodes.KeyUsed: return "That license key is active on another PC. Release it there first, or contact OpenXpert Solutions." + unchanged;
                case ExitCodes.RateLimited: return "The OpenXpert portal has received too many attempts. Please try again later." + unchanged;
                case ExitCodes.Network: return "TallyDues could not reach the OpenXpert portal. Check your internet connection and try again." + unchanged;
                case ExitCodes.BadResponse: return "The answer from the OpenXpert portal could not be verified, so it was ignored. Please try again later." + unchanged;
                case ExitCodes.NotConfigured: return "This is a test build of TallyDues, which cannot check license keys." + unchanged;
                default: return "TallyDues could not finish changing the key. Please contact OpenXpert Solutions." + unchanged;
            }
        }

        // A key that cannot be decrypted belongs to another Windows account, which may not replace it.
        // No license file at all is allowed: the portal still has to accept the new key.
        private int ReadCurrent(out string oldKey)
        {
            oldKey = null;
            CurrentEnd = null;
            LicenseFile license = store.Load();
            if (license == null) return ExitCodes.Ok;
            try
            {
                oldKey = LicenseStore.Unprotect(license.KeyProtected);
            }
            catch (CryptographicException)
            {
                return ExitCodes.OtherAccount;
            }
            catch (FormatException)
            {
                return ExitCodes.OtherAccount;
            }
            catch (ArgumentNullException)
            {
                return ExitCodes.OtherAccount;
            }
            try
            {
                if (license.IsAuthentic()) CurrentEnd = license.ValidUntil();
            }
            catch (Exception)
            {
                // Display only: a damaged date just shows no comparison.
            }
            return ExitCodes.Ok;
        }

        private void RequestToken(string key)
        {
            string nonce = Machine.Nonce(machine, Program.NewRandom());
            SignedAnswer answer = Portal.Post("/api/v1/tally/validate", Program.Request("key", key, machine, nonce), nonce);
            string answerToken = Portal.AsString(answer.Payload, "confirm_token");
            DateTimeOffset end;
            if (answerToken.Length != 48
                || !DateTimeOffset.TryParse(Portal.AsString(answer.Payload, "valid_until"), CultureInfo.InvariantCulture, DateTimeStyles.None, out end)
                || end <= new DateTimeOffset(Program.Now()))
            {
                throw new PortalException(ExitCodes.BadResponse);
            }
            token = answerToken;
            NewEnd = end;
        }

        // State is unsigned, so this only spares the user a portal block. The portal's limit is the real one.
        private bool IsLockedOut()
        {
            Dictionary<string, string> state = store.LoadState();
            string fails, at;
            int count;
            long ticks;
            if (!state.TryGetValue("KEY_FAILS", out fails) || !int.TryParse(fails, out count) || count < MaxTries) return false;
            if (!state.TryGetValue("KEY_FAIL_AT", out at) || !long.TryParse(at, out ticks)) return false;
            TimeSpan waited = Program.Now() - new DateTime(ticks);
            return waited >= TimeSpan.Zero && waited < LockTime;
        }

        private void CountFailure()
        {
            Program.WithLock<object>(() =>
            {
                Dictionary<string, string> state = store.LoadState();
                string fails;
                int count;
                if (!state.TryGetValue("KEY_FAILS", out fails) || !int.TryParse(fails, out count) || count >= MaxTries) count = 0;
                state["KEY_FAILS"] = (count + 1).ToString(CultureInfo.InvariantCulture);
                state["KEY_FAIL_AT"] = Program.Now().Ticks.ToString(CultureInfo.InvariantCulture);
                store.SaveState(state);
                return null;
            });
        }

        private void ClearFailures()
        {
            Program.WithLock<object>(() =>
            {
                Dictionary<string, string> state = store.LoadState();
                state.Remove("KEY_FAILS");
                state.Remove("KEY_FAIL_AT");
                store.SaveState(state);
                return null;
            });
        }
    }
}

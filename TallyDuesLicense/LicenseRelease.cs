// Frees this PC's seat on the portal and removes the license here. The portal's answer comes first:
// a failed call leaves TallyDues working. Portal release is safe to repeat, so Retry covers a lost answer.
using System;
using System.IO;
using System.Security.Cryptography;

namespace TallyDues.License
{
    internal static class LicenseRelease
    {
        public static int Run(string dir)
        {
            var store = new LicenseStore(dir);
            LicenseFile license = store.Load();
            if (license == null) return ExitCodes.NoLicense;
            string key;
            try
            {
                key = LicenseStore.Unprotect(license.KeyProtected);
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
            // The portal answers ok even when no machine matches, so a copied license must stop here.
            string[] machine = Machine.Components();
            if (!Machine.IsSameMachine(license.Components, machine)) return ExitCodes.OtherMachine;
            try
            {
                if (!store.LicenseCanBeReplaced()) return ExitCodes.ReleaseUnfinished;
            }
            catch (IOException)
            {
                return ExitCodes.ReleaseUnfinished;
            }
            catch (UnauthorizedAccessException)
            {
                return ExitCodes.ReleaseUnfinished;
            }

            string nonce = Machine.Nonce(machine, Program.NewRandom());
            Portal.Post("/api/v1/tally/release", Program.Request("key", key, machine, nonce), nonce);

            try
            {
                Program.WithLock<object>(() =>
                {
                    // The key may have been changed during the portal call. A new key's license stays.
                    LicenseFile latest = store.Load();
                    if (latest != null && latest.KeyProtected == license.KeyProtected) store.RemoveLicense();
                    return null;
                });
            }
            catch (IOException)
            {
                return ExitCodes.ReleaseUnfinished;
            }
            catch (UnauthorizedAccessException)
            {
                return ExitCodes.ReleaseUnfinished;
            }
            Program.RemoveRenewTask();
            return ExitCodes.Ok;
        }

        public static string Message(int code)
        {
            const string unchanged = " Your license was not released, and TallyDues keeps working on this PC.";
            switch (code)
            {
                case ExitCodes.NoLicense: return "There is no license on this PC to release. If you think this is wrong, please contact OpenXpert Solutions.";
                case ExitCodes.OtherAccount: return "This license was set up from another Windows account. Sign in to that account and try again." + unchanged;
                case ExitCodes.OtherMachine: return "This license belongs to a different PC, so it cannot be released from here. Please contact OpenXpert Solutions.";
                case ExitCodes.Network: return "TallyDues could not reach the OpenXpert portal. Check your internet connection and try again. If this PC has no internet, please call OpenXpert Solutions and ask them to release your license." + unchanged;
                case ExitCodes.RateLimited: return "The OpenXpert portal has received too many attempts. Please try again later." + unchanged;
                case ExitCodes.BadResponse: return "The answer from the OpenXpert portal could not be verified, so it was ignored. Please try again later." + unchanged;
                case ExitCodes.ReleaseUnfinished: return "TallyDues could not change its license file. Close TallyPrime, then try again.";
                case ExitCodes.NotConfigured: return "This is a test build of TallyDues, which cannot release a license." + unchanged;
                default: return "TallyDues could not release your license. Please contact OpenXpert Solutions." + unchanged;
            }
        }
    }
}

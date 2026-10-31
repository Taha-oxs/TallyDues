// Build values for the license helper. build\Build-Helper.ps1 replaces the placeholders.
// A release build refuses to run while either placeholder is left.
namespace TallyDues.License
{
    internal static class Config
    {
        public const string PortalUrl = "__PORTAL_URL__";
        public const string PublicKey = "__ED25519_PUBLIC_KEY__";
        public const string Version = "1.0.0";
        // Fixed so that one machine gives the same hashes on every install.
        public const string MachineSalt = "TallyDues-machine-v1";

        public static bool IsConfigured
        {
            get { return !PortalUrl.StartsWith("__") && !PublicKey.StartsWith("__"); }
        }
    }
}

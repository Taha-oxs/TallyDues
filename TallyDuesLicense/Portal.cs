// HTTPS calls to the portal and checks on its signed answers.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace TallyDues.License
{
    internal sealed class SignedAnswer
    {
        public string PayloadRaw;
        public string Signature;
        public Dictionary<string, object> Payload;
    }

    internal sealed class PortalException : Exception
    {
        public readonly int ExitCode;
        public PortalException(int exitCode) : base("portal error " + exitCode) { ExitCode = exitCode; }
    }

    internal static class Portal
    {
        private static readonly Dictionary<string, int> ErrorCodes = new Dictionary<string, int>
        {
            { "invalid_key", ExitCodes.InvalidKey },
            { "expired", ExitCodes.KeyExpired },
            { "revoked", ExitCodes.Revoked },
            { "subscription_suspended", ExitCodes.Suspended },
            { "key_used", ExitCodes.KeyUsed },
            { "limit_reached", ExitCodes.KeyUsed },
            { "rate_limited", ExitCodes.RateLimited }
        };

        public static SignedAnswer Post(string endpoint, Dictionary<string, object> body, string expectedNonce)
        {
            return Post(endpoint, body, expectedNonce, "ok");
        }

        // Only a signed answer may carry a result other than "ok", such as heartbeat's "released".
        public static SignedAnswer Post(string endpoint, Dictionary<string, object> body, string expectedNonce, params string[] results)
        {
            string json = new JavaScriptSerializer().Serialize(body);
            string text = Send(endpoint, json);
            SignedAnswer answer = Read(text);
            if (!IsTrusted(answer.PayloadRaw, answer.Signature)) throw new PortalException(ExitCodes.BadResponse);
            if (Array.IndexOf(results, AsString(answer.Payload, "result")) < 0) throw new PortalException(ExitCodes.BadResponse);
            if (AsString(answer.Payload, "nonce") != expectedNonce) throw new PortalException(ExitCodes.BadResponse);
            return answer;
        }

        public static bool IsTrusted(string payloadRaw, string signature)
        {
            try
            {
                return Ed25519.Verify(Convert.FromBase64String(Keys.PublicKey), Encoding.UTF8.GetBytes(payloadRaw), Convert.FromBase64String(signature));
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static string AsString(Dictionary<string, object> values, string name)
        {
            object value;
            return values != null && values.TryGetValue(name, out value) && value != null ? value.ToString() : "";
        }

        private static string Send(string endpoint, string json)
        {
            if (!Config.IsConfigured) throw new PortalException(ExitCodes.NotConfigured);
#if !TEST_BUILD && !LOCAL_PORTAL
            if (!Config.PortalUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) throw new PortalException(ExitCodes.NotConfigured);
#endif
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var request = (HttpWebRequest)WebRequest.Create(Config.PortalUrl.TrimEnd('/') + endpoint);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.UserAgent = "TallyDues/" + Config.Version;
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            request.AllowAutoRedirect = false;
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            try
            {
                using (Stream stream = request.GetRequestStream()) stream.Write(bytes, 0, bytes.Length);
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    // A redirect would end here too: it has no signed answer, so Read rejects it.
                    return ReadBody(response);
                }
            }
            catch (WebException e)
            {
                var response = e.Response as HttpWebResponse;
                if (response == null) throw new PortalException(ExitCodes.Network);
                using (response) throw new PortalException(ErrorFrom(response));
            }
            catch (IOException)
            {
                throw new PortalException(ExitCodes.Network);
            }
        }

        private static int ErrorFrom(HttpWebResponse response)
        {
            if ((int)response.StatusCode >= 500) return ExitCodes.Network;
            // Laravel's own throttle answers 429 with no "error" field.
            if ((int)response.StatusCode == 429) return ExitCodes.RateLimited;
            try
            {
                var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(ReadBody(response));
                int code;
                if (ErrorCodes.TryGetValue(AsString(values, "error"), out code)) return code;
            }
            catch (Exception)
            {
                // Falls through: anything else from the server is a bad response.
            }
            return ExitCodes.BadResponse;
        }

        private static string ReadBody(HttpWebResponse response)
        {
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static SignedAnswer Read(string text)
        {
            try
            {
                var values = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(text);
                // Parsed from the same text the signature check uses, so a second "payload" cannot differ.
                string raw = RawPayload(text);
                var answer = new SignedAnswer
                {
                    PayloadRaw = raw,
                    Signature = AsString(values, "signature"),
                    Payload = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(raw)
                };
                if (answer.Payload == null || answer.Signature.Length == 0) throw new PortalException(ExitCodes.BadResponse);
                return answer;
            }
            catch (PortalException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new PortalException(ExitCodes.BadResponse);
            }
        }

        // The signature covers the exact JSON text of "payload", so it is cut out, not rebuilt.
        // PHP signs with unescaped slashes, and Laravel then escapes them in the response.
        public static string RawPayload(string text)
        {
            const string marker = "\"payload\":";
            int start = text.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) throw new PortalException(ExitCodes.BadResponse);
            start += marker.Length;
            int depth = 0;
            bool inString = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\') i++;
                    else if (c == '"') inString = false;
                }
                else if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                {
                    return text.Substring(start, i - start + 1).Replace("\\/", "/");
                }
            }
            throw new PortalException(ExitCodes.BadResponse);
        }
    }

    internal static class Keys
    {
        public static string PublicKey
        {
            get
            {
#if TEST_BUILD
                string fake = Environment.GetEnvironmentVariable("TD_TEST_PUBLIC_KEY");
                if (!string.IsNullOrEmpty(fake)) return fake;
#endif
                return Config.PublicKey;
            }
        }
    }
}

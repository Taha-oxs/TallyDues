// RFC 8032 Ed25519 signature verification. .NET Framework has none built in.
// Verification handles only public data, so it does not need to run in constant time.
using System;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace TallyDues.License
{
    internal static class Ed25519
    {
        private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
        private static readonly BigInteger L = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
        private static readonly BigInteger D = Mod(-121665 * Inv(121666));
        private static readonly BigInteger I = BigInteger.ModPow(2, (P - 1) / 4, P);
        private static readonly BigInteger[] Base = MakeBase();

        public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
        {
            if (publicKey == null || signature == null || message == null) return false;
            if (publicKey.Length != 32 || signature.Length != 64) return false;

            byte[] r = signature.Take(32).ToArray();
            BigInteger s = FromBytes(signature.Skip(32).ToArray());
            if (s >= L) return false;

            BigInteger[] a = Decompress(publicKey);
            if (a == null || Decompress(r) == null) return false;

            BigInteger h = FromBytes(Sha512(r, publicKey, message)) % L;
            BigInteger[] negA = { Mod(-a[0]), a[1], a[2], Mod(-a[3]) };
            BigInteger[] check = Add(Multiply(s, Base), Multiply(h, negA));
            return Encode(check).SequenceEqual(r);
        }

#if TEST_BUILD
        // Test-only signing, so the mock portal can answer like the real one.
        public static byte[] PublicFromSeed(byte[] seed)
        {
            return Encode(Multiply(Clamp(Sha512(seed)), Base));
        }

        public static byte[] Sign(byte[] seed, byte[] message)
        {
            byte[] hash = Sha512(seed);
            BigInteger a = Clamp(hash);
            byte[] prefix = hash.Skip(32).ToArray();
            byte[] publicKey = Encode(Multiply(a, Base));
            BigInteger r = FromBytes(Sha512(prefix, message)) % L;
            byte[] rEncoded = Encode(Multiply(r, Base));
            BigInteger h = FromBytes(Sha512(rEncoded, publicKey, message)) % L;
            BigInteger s = (r + h * a) % L;
            return rEncoded.Concat(ToBytes(s)).ToArray();
        }

        private static BigInteger Clamp(byte[] hash)
        {
            byte[] low = hash.Take(32).ToArray();
            low[0] &= 248;
            low[31] &= 127;
            low[31] |= 64;
            return FromBytes(low);
        }
#endif

        private static BigInteger Mod(BigInteger value)
        {
            BigInteger result = BigInteger.Remainder(value, P);
            return result.Sign < 0 ? result + P : result;
        }

        private static BigInteger Inv(BigInteger value)
        {
            return BigInteger.ModPow(value, P - 2, P);
        }

        private static BigInteger[] MakeBase()
        {
            BigInteger y = Mod(4 * Inv(5));
            BigInteger x = RecoverX(y, 0);
            return new[] { x, y, BigInteger.One, Mod(x * y) };
        }

        private static BigInteger RecoverX(BigInteger y, int sign)
        {
            BigInteger y2 = Mod(y * y);
            BigInteger x2 = Mod((y2 - 1) * Inv(Mod(D * y2 + 1)));
            BigInteger x = BigInteger.ModPow(x2, (P + 3) / 8, P);
            if (Mod(x * x - x2) != 0) x = Mod(x * I);
            if (Mod(x * x - x2) != 0) return BigInteger.MinusOne;
            if (x.IsZero && sign == 1) return BigInteger.MinusOne;
            if ((int)(x % 2) != sign) x = P - x;
            return x;
        }

        private static BigInteger[] Decompress(byte[] encoded)
        {
            byte[] copy = (byte[])encoded.Clone();
            int sign = (copy[31] >> 7) & 1;
            copy[31] &= 127;
            BigInteger y = FromBytes(copy);
            if (y >= P) return null;
            BigInteger x = RecoverX(y, sign);
            if (x.Sign < 0) return null;
            return new[] { x, y, BigInteger.One, Mod(x * y) };
        }

        private static byte[] Encode(BigInteger[] point)
        {
            BigInteger z = Inv(point[2]);
            BigInteger x = Mod(point[0] * z);
            BigInteger y = Mod(point[1] * z);
            byte[] bytes = ToBytes(y);
            if (!x.IsEven) bytes[31] |= 128;
            return bytes;
        }

        private static BigInteger[] Add(BigInteger[] p, BigInteger[] q)
        {
            BigInteger a = Mod((p[1] - p[0]) * (q[1] - q[0]));
            BigInteger b = Mod((p[1] + p[0]) * (q[1] + q[0]));
            BigInteger c = Mod(2 * p[3] * q[3] * D);
            BigInteger d = Mod(2 * p[2] * q[2]);
            BigInteger e = b - a, f = d - c, g = d + c, h = b + a;
            return new[] { Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h) };
        }

        private static BigInteger[] Multiply(BigInteger scalar, BigInteger[] point)
        {
            BigInteger[] result = { BigInteger.Zero, BigInteger.One, BigInteger.One, BigInteger.Zero };
            BigInteger[] current = point;
            while (scalar.Sign > 0)
            {
                if (!scalar.IsEven) result = Add(result, current);
                current = Add(current, current);
                scalar >>= 1;
            }
            return result;
        }

        private static BigInteger FromBytes(byte[] littleEndian)
        {
            return new BigInteger(littleEndian.Concat(new byte[] { 0 }).ToArray());
        }

        private static byte[] ToBytes(BigInteger value)
        {
            byte[] raw = value.ToByteArray();
            byte[] fixedSize = new byte[32];
            Array.Copy(raw, fixedSize, Math.Min(raw.Length, 32));
            return fixedSize;
        }

        private static byte[] Sha512(params byte[][] parts)
        {
            using (SHA512 sha = SHA512.Create())
            {
                return sha.ComputeHash(parts.SelectMany(part => part).ToArray());
            }
        }
    }
}

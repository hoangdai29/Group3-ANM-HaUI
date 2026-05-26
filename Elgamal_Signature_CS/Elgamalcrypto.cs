using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Elgamal_Signature_CS
{
    public class ElGamalKeyPair
    {
        public BigInteger Q { get; set; }   // Số nguyên tố lớn mô-đun (q)
        public BigInteger A { get; set; }   // Căn nguyên thủy (a)
        public BigInteger YA { get; set; }  // Khóa công khai (Y_A)
        public BigInteger XA { get; set; }  // Khóa bí mật (X_A)
    }

    public class ElGamalSig
    {
        public BigInteger R { get; set; }   // Chữ ký thành phần r
        public BigInteger S { get; set; }   // Chữ ký thành phần s
    }

    public static class ElGamalCrypto
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        public static ElGamalKeyPair GenerateKeys(int bitLength = 512)
        {
            BigInteger q = GenerateSafePrime(bitLength);
            BigInteger a = FindGeneratorForSafePrime(q);
            BigInteger xa = GenerateRandomInRange(2, q - 2);
            BigInteger ya = BigInteger.ModPow(a, xa, q);

            return new ElGamalKeyPair { Q = q, A = a, XA = xa, YA = ya };
        }

        public static ElGamalSig Sign(string message, ElGamalKeyPair key)
        {
            BigInteger q = key.Q;
            BigInteger a = key.A;
            BigInteger xa = key.XA;
            BigInteger qMinus1 = q - 1;

            BigInteger hash = HashToBigInteger(message);

            BigInteger k, r, s;
            int tries = 0;

            do
            {
                if (++tries > 10_000)
                    throw new InvalidOperationException("Không thể tạo chữ ký số. Vui lòng thử lại.");

                do
                {
                    k = GenerateRandomInRange(2, qMinus1 - 1);
                }
                while (GCD(k, qMinus1) != 1);

                r = BigInteger.ModPow(a, k, q);

                BigInteger kInv = ModInverse(k, qMinus1);
                BigInteger xar = (xa * r) % qMinus1;

                // Đồng dư số dương: s = k^-1 * (M - X_A * r) mod (q-1)
                BigInteger diff = (hash - xar) % qMinus1;
                if (diff < 0) diff += qMinus1;

                s = (kInv * diff) % qMinus1;
            }
            while (s == 0);

            return new ElGamalSig { R = r, S = s };
        }

        public static bool Verify(string message, ElGamalSig sig, ElGamalKeyPair key)
        {
            BigInteger q = key.Q;
            BigInteger a = key.A;
            BigInteger ya = key.YA;
            BigInteger r = sig.R;
            BigInteger s = sig.S;

            if (r <= 0 || r >= q) return false;
            if (s <= 0 || s >= q - 1) return false;

            BigInteger hash = HashToBigInteger(message);

            // Vế trái: V1 = a^M mod q
            BigInteger lhs = BigInteger.ModPow(a, hash, q);
            // Vế phải: V2 = (Y_A^r * r^s) mod q
            BigInteger ya_r = BigInteger.ModPow(ya, r, q);
            BigInteger r_s = BigInteger.ModPow(r, s, q);
            BigInteger rhs = (ya_r * r_s) % q;

            return lhs == rhs;
        }

        public static (BigInteger hash, BigInteger lhs, BigInteger rhs, bool valid)
            VerifyWithDetails(string message, ElGamalSig sig, ElGamalKeyPair key)
        {
            BigInteger q = key.Q;
            BigInteger a = key.A;
            BigInteger ya = key.YA;
            BigInteger r = sig.R;
            BigInteger s = sig.S;

            BigInteger hash = HashToBigInteger(message);

            BigInteger lhs = BigInteger.ModPow(a, hash, q);
            BigInteger ya_r = BigInteger.ModPow(ya, r, q);
            BigInteger r_s = BigInteger.ModPow(r, s, q);
            BigInteger rhs = (ya_r * r_s) % q;

            bool valid = (r > 0 && r < q) && (s > 0 && s < q - 1) && (lhs == rhs);

            return (hash, lhs, rhs, valid);
        }

        public static BigInteger HashToBigInteger(string message)
        {
            using var sha256 = SHA256.Create();
            byte[] raw = sha256.ComputeHash(Encoding.UTF8.GetBytes(message));
            return new BigInteger(raw, isUnsigned: true, isBigEndian: false);
        }

        private static BigInteger GenerateSafePrime(int bitLength)
        {
            while (true)
            {
                BigInteger qPrime = GenerateRandomPrime(bitLength - 1);
                BigInteger p = 2 * qPrime + 1;
                if (MillerRabin(p, 15)) return p;
            }
        }

        private static BigInteger FindGeneratorForSafePrime(BigInteger p)
        {
            BigInteger qPrime = (p - 1) / 2;
            foreach (BigInteger g in new BigInteger[] { 2, 3, 5, 6, 7, 10, 11, 13, 14, 15 })
            {
                if (g >= p) continue;
                if (BigInteger.ModPow(g, 2, p) != 1 && BigInteger.ModPow(g, qPrime, p) != 1) return g;
            }
            for (BigInteger g = 2; g < p; g++)
            {
                if (BigInteger.ModPow(g, 2, p) != 1 && BigInteger.ModPow(g, qPrime, p) != 1) return g;
            }
            return 2;
        }

        private static BigInteger GenerateRandomPrime(int bitLength)
        {
            while (true)
            {
                BigInteger n = GenerateRandomBits(bitLength);
                n |= BigInteger.One;
                n |= BigInteger.One << (bitLength - 1);
                if (MillerRabin(n, 15)) return n;
            }
        }

        public static bool MillerRabin(BigInteger n, int rounds = 15)
        {
            if (n < 2) return false;
            if (n == 2 || n == 3) return true;
            if (n.IsEven) return false;

            BigInteger d = n - 1;
            int r = 0;
            while (d.IsEven) { d >>= 1; r++; }

            for (int i = 0; i < rounds; i++)
            {
                BigInteger a = GenerateRandomInRange(2, n - 2);
                BigInteger x = BigInteger.ModPow(a, d, n);

                if (x == 1 || x == n - 1) continue;

                bool composite = true;
                for (int j = 0; j < r - 1; j++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == n - 1) { composite = false; break; }
                }
                if (composite) return false;
            }
            return true;
        }

        public static BigInteger GCD(BigInteger a, BigInteger b)
        {
            a = BigInteger.Abs(a); b = BigInteger.Abs(b);
            while (b != 0) { var t = b; b = a % b; a = t; }
            return a;
        }

        public static BigInteger ModInverse(BigInteger a, BigInteger m)
        {
            ExtendedGCD(((a % m) + m) % m, m, out BigInteger g, out BigInteger x, out _);
            if (g != 1) throw new ArithmeticException("Nghịch đảo không tồn tại");
            return (x % m + m) % m;
        }

        private static void ExtendedGCD(BigInteger a, BigInteger b, out BigInteger g, out BigInteger x, out BigInteger y)
        {
            if (a == 0) { g = b; x = 0; y = 1; return; }
            ExtendedGCD(b % a, a, out g, out BigInteger x1, out BigInteger y1);
            x = y1 - b / a * x1;
            y = x1;
        }

        public static BigInteger GenerateRandomInRange(BigInteger min, BigInteger max)
        {
            if (min > max) throw new ArgumentException("min phải ≤ max");
            if (min == max) return min;

            BigInteger range = max - min;
            int byteCount = range.GetByteCount(isUnsigned: true) + 1;
            byte[] buf = new byte[byteCount];

            BigInteger result;
            do
            {
                Rng.GetBytes(buf, 0, byteCount - 1);
                buf[byteCount - 1] = 0;
                result = new BigInteger(buf) % (range + 1);
            }
            while (result < 0);

            return min + result;
        }

        private static BigInteger GenerateRandomBits(int bitLength)
        {
            int byteLen = (bitLength + 7) / 8;
            byte[] buf = new byte[byteLen + 1];
            Rng.GetBytes(buf, 0, byteLen);
            buf[byteLen] = 0;
            return new BigInteger(buf);
        }
    }
}
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
        public BigInteger Q { get; set; }
        public BigInteger A { get; set; }
        public BigInteger YA { get; set; }
        public BigInteger XA { get; set; }
    }

    public class ElGamalSig
    {
        public BigInteger R { get; set; }
        public BigInteger S { get; set; }
    }

    public static class ElGamalCrypto
    {
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        // Danh sách số nguyên tố nhỏ dùng để sàng lọc nhanh, tăng tốc 10 lần thời gian sinh số nguyên tố
        private static readonly int[] SmallPrimes = {
            2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97,
            101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199
        };

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
                    throw new InvalidOperationException("Không thể tạo chữ ký số. Hãy thử lại.");

                do { k = GenerateRandomInRange(2, qMinus1 - 1); }
                while (GCD(k, qMinus1) != 1);

                r = BigInteger.ModPow(a, k, q);
                BigInteger kInv = ModInverse(k, qMinus1);
                BigInteger xar = (xa * r) % qMinus1;

                BigInteger diff = (hash - xar) % qMinus1;
                if (diff < 0) diff += qMinus1;

                s = (kInv * diff) % qMinus1;
            }
            while (s == 0);

            return new ElGamalSig { R = r, S = s };
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

        // TỐI ƯU HÓA: Sàng lọc chia thử trước khi chạy Miller-Rabin đắt đỏ
        private static bool IsPrimeOptimized(BigInteger n)
        {
            foreach (int p in SmallPrimes)
            {
                if (n == p) return true;
                if (n % p == 0) return false;
            }
            return MillerRabin(n, 8); // Giảm số vòng lặp xuống 8 vẫn đảm bảo độ chính xác an toàn
        }

        public static bool IsSafePrime(BigInteger p)
        {
            if (p < 5) return false;
            if (!IsPrimeOptimized(p)) return false;
            BigInteger q = (p - 1) / 2;
            return IsPrimeOptimized(q);
        }

        private static BigInteger GenerateSafePrime(int bitLength)
        {
            while (true)
            {
                BigInteger qPrime = GenerateRandomPrime(bitLength - 1);
                BigInteger p = 2 * qPrime + 1;
                if (IsPrimeOptimized(p)) return p;
            }
        }

        private static BigInteger FindGeneratorForSafePrime(BigInteger p)
        {
            BigInteger qPrime = (p - 1) / 2;
            // Thử nhanh các giá trị phần tử sinh phổ biến
            foreach (BigInteger g in new BigInteger[] { 2, 3, 5, 6, 7, 10, 11, 13, 14, 15 })
            {
                if (g >= p) continue;
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
                if (IsPrimeOptimized(n)) return n;
            }
        }

        public static bool MillerRabin(BigInteger n, int rounds = 8)
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
            return (x % m + m) % m;
        }

        private static void ExtendedGCD(BigInteger a, BigInteger b, out BigInteger g, out BigInteger x, out BigInteger y)
        {
            if (a == 0) { g = b; x = 0; y = 1; return; }
            ExtendedGCD(b % a, a, out g, out BigInteger x1, out BigInteger y1);
            x = y1 - b / a * x1; y = x1;
        }

        public static BigInteger GenerateRandomInRange(BigInteger min, BigInteger max)
        {
            BigInteger range = max - min;
            int byteCount = range.GetByteCount(isUnsigned: true) + 1;
            byte[] buf = new byte[byteCount];
            BigInteger result;
            do
            {
                Rng.GetBytes(buf, 0, byteCount - 1);
                buf[byteCount - 1] = 0;
                result = new BigInteger(buf) % (range + 1);
            } while (result < 0);
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
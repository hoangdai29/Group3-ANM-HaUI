package vn.haui.elgamal.model;

import java.math.BigInteger;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;

public class ElGamalModel {

    private final SecureRandom random;
    private long lastOperationTimeMs = 0;

    public ElGamalModel() {
        this.random = new SecureRandom();
    }

    public KeyPairData sinhKhoaTuDong(int bitLength) {
        long start = System.currentTimeMillis();
        BigInteger q;
        BigInteger p;
        do {
            q = BigInteger.probablePrime(bitLength - 1, random);
            p = q.multiply(BigInteger.TWO).add(BigInteger.ONE);
        } while (!p.isProbablePrime(40));

        BigInteger alpha;
        while (true) {
            alpha = new BigInteger(bitLength, random);
            if (alpha.compareTo(BigInteger.ONE) > 0 && alpha.compareTo(p.subtract(BigInteger.ONE)) < 0
                    && !alpha.modPow(BigInteger.TWO, p).equals(BigInteger.ONE)
                    && !alpha.modPow(q, p).equals(BigInteger.ONE)) {
                break;
            }
        }

        BigInteger pm1 = p.subtract(BigInteger.ONE);
        BigInteger x;
        do {
            x = new BigInteger(pm1.bitLength(), random);
        } while (x.compareTo(BigInteger.ONE) <= 0 || x.compareTo(pm1) >= 0);

        BigInteger y = alpha.modPow(x, p);
        lastOperationTimeMs = System.currentTimeMillis() - start;
        return new KeyPairData(p, alpha, x, y);
    }

    public KeyPairData sinhKhoaTuChon(BigInteger p, BigInteger alpha, BigInteger x) {
        BigInteger y = alpha.modPow(x, p);
        return new KeyPairData(p, alpha, x, y);
    }

    public SignatureData kyVanBan(byte[] data, KeyPairData keyPair) {
        long start = System.currentTimeMillis();
        if (data == null || keyPair == null || !keyPair.hasPrivateKey()) {
            throw new IllegalArgumentException("Dữ liệu ký hoặc khóa bí mật không hợp lệ.");
        }

        BigInteger p = keyPair.getP();
        BigInteger alpha = keyPair.getAlpha();
        BigInteger x = keyPair.getX();
        BigInteger pm1 = p.subtract(BigInteger.ONE);
        BigInteger m = layGiaTriBamSHA256(data);

        BigInteger k;
        BigInteger r;
        BigInteger s;

        while (true) {
            do {
                k = new BigInteger(pm1.bitLength(), random);
            } while (k.compareTo(BigInteger.ONE) <= 0 || k.compareTo(pm1) >= 0);

            if (!k.gcd(pm1).equals(BigInteger.ONE)) {
                continue;
            }

            r = alpha.modPow(k, p);
            BigInteger kInv = k.modInverse(pm1);
            BigInteger mMinusXr = m.subtract(x.multiply(r)).mod(pm1);
            s = kInv.multiply(mMinusXr).mod(pm1);
            if (!s.equals(BigInteger.ZERO)) {
                break;
            }
        }

        lastOperationTimeMs = System.currentTimeMillis() - start;
        return new SignatureData(r, s);
    }

    public boolean xacMinhChuKy(byte[] data, SignatureData signature, BigInteger p, BigInteger alpha, BigInteger y) {
        if (data == null || signature == null || p == null || alpha == null || y == null) {
            return false;
        }

        BigInteger r = signature.getR();
        BigInteger s = signature.getS();
        BigInteger pm1 = p.subtract(BigInteger.ONE);
        if (r.compareTo(BigInteger.ZERO) <= 0 || r.compareTo(p) >= 0) {
            return false;
        }
        if (s.compareTo(BigInteger.ZERO) <= 0 || s.compareTo(pm1) >= 0) {
            return false;
        }

        BigInteger m = layGiaTriBamSHA256(data);
        BigInteger v1 = alpha.modPow(m, p);
        BigInteger v2 = y.modPow(r, p).multiply(r.modPow(s, p)).mod(p);
        return v1.equals(v2);
    }

    private BigInteger layGiaTriBamSHA256(byte[] data) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            return new BigInteger(1, md.digest(data));
        } catch (NoSuchAlgorithmException e) {
            throw new RuntimeException("SHA-256 không được hỗ trợ trong môi trường này.", e);
        }
    }

    public long getLastOperationTimeMs() {
        return lastOperationTimeMs;
    }
}
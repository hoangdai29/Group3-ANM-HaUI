package vn.haui.elgamal.service;

import vn.haui.elgamal.model.SignatureData;

import java.math.BigInteger;
import java.util.Base64;

public class ValidationService {

    public static final int MAX_TEXT_LENGTH = 1_000_000;

    public static void validateTextLength(String text) {
        if (text != null && text.length() > MAX_TEXT_LENGTH) {
            throw new IllegalArgumentException(
                    "Văn bản quá dài (" + text.length() + " ký tự). Giới hạn tối đa là " + MAX_TEXT_LENGTH + " ký tự."
            );
        }
    }

    public static boolean isPrime(BigInteger n) {
        return n != null && n.compareTo(BigInteger.ONE) > 0 && n.isProbablePrime(40);
    }

    public static boolean isPrimitiveRoot(BigInteger alpha, BigInteger p) {
        if (alpha == null || p == null) {
            return false;
        }
        if (alpha.compareTo(BigInteger.ONE) <= 0 || alpha.compareTo(p.subtract(BigInteger.ONE)) >= 0) {
            return false;
        }
        BigInteger pm1 = p.subtract(BigInteger.ONE);
        if (!alpha.modPow(pm1, p).equals(BigInteger.ONE)) {
            return false;
        }
        BigInteger q = pm1.divide(BigInteger.TWO);
        if (q.isProbablePrime(40)) {
            return !alpha.modPow(BigInteger.TWO, p).equals(BigInteger.ONE)
                    && !alpha.modPow(q, p).equals(BigInteger.ONE);
        }

        int[] smallPrimes = {2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97};
        for (int factor : smallPrimes) {
            BigInteger bFactor = BigInteger.valueOf(factor);
            if (pm1.mod(bFactor).equals(BigInteger.ZERO)
                    && alpha.modPow(pm1.divide(bFactor), p).equals(BigInteger.ONE)) {
                return false;
            }
        }
        return true;
    }

    public static boolean isHeuristicPrimitiveRootCheck(BigInteger p) {
        if (p == null) {
            return false;
        }
        BigInteger q = p.subtract(BigInteger.ONE).divide(BigInteger.TWO);
        return !q.isProbablePrime(40);
    }

    public static boolean isValidPrivateKey(BigInteger x, BigInteger p) {
        return x != null && p != null && x.compareTo(BigInteger.ONE) > 0 && x.compareTo(p.subtract(BigInteger.ONE)) < 0;
    }

    public static SignatureData parseSignature(String sigStr) {
        if (sigStr == null || sigStr.trim().isEmpty()) {
            throw new IllegalArgumentException("Chữ ký không được để trống.");
        }
        String[] parts = sigStr.trim().split("\\|");
        if (parts.length != 2) {
            throw new IllegalArgumentException("Sai định dạng chữ ký. Định dạng chuẩn phải có dạng: r_base64|s_base64");
        }
        try {
            byte[] rBytes = Base64.getDecoder().decode(parts[0].trim());
            byte[] sBytes = Base64.getDecoder().decode(parts[1].trim());
            BigInteger r = new BigInteger(rBytes);
            BigInteger s = new BigInteger(sBytes);
            return new SignatureData(r, s);
        } catch (IllegalArgumentException e) {
            throw new IllegalArgumentException("Chuỗi Base64 của chữ ký không hợp lệ.");
        } catch (Exception e) {
            throw new IllegalArgumentException("Định dạng dữ liệu số bên trong chữ ký không hợp lệ.");
        }
    }
}
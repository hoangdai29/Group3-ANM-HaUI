package vn.haui.elgamal.model;

import java.math.BigInteger;

/**
 * Lớp dữ liệu lưu trữ thông tin chữ ký số ElGamal.
 * Bao gồm cặp giá trị (r, s) được sinh ra từ quá trình ký tài liệu.
 */
public class SignatureData {
    private final BigInteger r; // Thành phần chữ ký r = alpha^k mod p
    private final BigInteger s; // Thành phần chữ ký s = k^-1 * (m - x*r) mod (p-1)

    public SignatureData(BigInteger r, BigInteger s) {
        this.r = r;
        this.s = s;
    }

    public BigInteger getR() {
        return r;
    }

    public BigInteger getS() {
        return s;
    }
}

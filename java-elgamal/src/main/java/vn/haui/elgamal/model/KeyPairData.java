package vn.haui.elgamal.model;

import java.math.BigInteger;

/**
 * Lớp dữ liệu lưu trữ thông tin cặp khóa trong hệ mật chữ ký số ElGamal.
 * Tuân thủ mô hình MVC sạch bằng cách đóng gói các tham số toán học thành một thực thể dữ liệu.
 */
public class KeyPairData {
    private final BigInteger p;      // Số nguyên tố lớn p
    private final BigInteger alpha;  // Căn nguyên thủy alpha
    private final BigInteger x;      // Khóa bí mật x (có thể null nếu chỉ lưu khóa công khai)
    private final BigInteger y;      // Khóa công khai y = alpha^x mod p

    /**
     * Khởi tạo đối tượng cặp khóa đầy đủ (bao gồm cả khóa bí mật và khóa công khai).
     */
    public KeyPairData(BigInteger p, BigInteger alpha, BigInteger x, BigInteger y) {
        this.p = p;
        this.alpha = alpha;
        this.x = x;
        this.y = y;
    }

    /**
     * Khởi tạo đối tượng chỉ chứa thông tin khóa công khai (khóa bí mật x là null).
     */
    public KeyPairData(BigInteger p, BigInteger alpha, BigInteger y) {
        this(p, alpha, null, y);
    }

    public BigInteger getP() {
        return p;
    }

    public BigInteger getAlpha() {
        return alpha;
    }

    public BigInteger getX() {
        return x;
    }

    public BigInteger getY() {
        return y;
    }

    /**
     * Kiểm tra xem cặp khóa này có chứa khóa bí mật x hay không.
     */
    public boolean hasPrivateKey() {
        return x != null;
    }
}

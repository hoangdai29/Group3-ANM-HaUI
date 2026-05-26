package vn.haui.elgamal.model;

import java.math.BigInteger;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;

/**
 * Lớp logic toán học thuần túy cho Hệ mật Chữ Ký Số ElGamal.
 * Không chứa bất kỳ tham chiếu hay thư viện nào liên quan tới JavaFX để đảm bảo đúng kiến trúc MVC.
 */
public class ElGamalModel {

    private final SecureRandom random;

    public ElGamalModel() {
        this.random = new SecureRandom();
    }

    /**
     * Sinh khóa tự động sử dụng số nguyên tố an toàn (Safe Prime) để việc sinh nhanh và đảm bảo độ chính xác toán học.
     * 
     * @param bitLength Độ dài bit yêu cầu (512-bit hoặc 1024-bit)
     * @return Một đối tượng KeyPairData chứa đầy đủ p, alpha, x, y
     */
    public KeyPairData sinhKhoaTuDong(int bitLength) {
        // 1. Sinh số nguyên tố an toàn p = 2q + 1 với q là số nguyên tố lớn
        BigInteger q;
        BigInteger p;
        do {
            // q có độ dài bit là (bitLength - 1)
            q = BigInteger.probablePrime(bitLength - 1, random);
            p = q.multiply(BigInteger.TWO).add(BigInteger.ONE);
        } while (!p.isProbablePrime(40)); // Xác nhận p cũng là số nguyên tố

        // 2. Tìm căn nguyên thủy alpha modulo p
        // Vì p là số nguyên tố an toàn nên các ước số của p-1 chỉ gồm: 1, 2, q, 2q.
        // Bất kỳ số alpha nào thuộc [2, p-1] đều là căn nguyên thủy nếu: alpha^2 != 1 mod p và alpha^q != 1 mod p.
        BigInteger alpha;
        while (true) {
            alpha = new BigInteger(bitLength, random);
            // Ràng buộc 1 < alpha < p-1
            if (alpha.compareTo(BigInteger.ONE) > 0 && alpha.compareTo(p.subtract(BigInteger.ONE)) < 0) {
                // Kiểm tra xem alpha^2 mod p != 1
                if (!alpha.modPow(BigInteger.TWO, p).equals(BigInteger.ONE)) {
                    // Kiểm tra xem alpha^q mod p != 1
                    if (!alpha.modPow(q, p).equals(BigInteger.ONE)) {
                        break; // alpha chính là căn nguyên thủy hợp lệ
                    }
                }
            }
        }

        // 3. Chọn ngẫu nhiên khóa bí mật x trong khoảng: 1 < x < p-1
        BigInteger pm1 = p.subtract(BigInteger.ONE);
        BigInteger x;
        do {
            x = new BigInteger(pm1.bitLength(), random);
        } while (x.compareTo(BigInteger.ONE) <= 0 || x.compareTo(pm1) >= 0);

        // 4. Tính khóa công khai y = alpha^x mod p
        BigInteger y = alpha.modPow(x, p);

        return new KeyPairData(p, alpha, x, y);
    }

    /**
     * Sinh khóa công khai y từ các thông tin p, alpha, x do người dùng tự chọn.
     * 
     * @return Một đối tượng KeyPairData mới chứa đầy đủ p, alpha, x, y
     */
    public KeyPairData sinhKhoaTuChon(BigInteger p, BigInteger alpha, BigInteger x) {
        BigInteger y = alpha.modPow(x, p);
        return new KeyPairData(p, alpha, x, y);
    }

    /**
     * Thuật toán ký chữ ký số ElGamal trên mảng byte dữ liệu nhị phân.
     * 
     * @param data Dữ liệu mảng byte đầu vào (của văn bản hoặc tệp tin bất kỳ)
     * @param keyPair Cặp khóa hợp lệ chứa khóa bí mật x để ký
     * @return Đối tượng SignatureData chứa cặp chữ ký số (r, s)
     * @throws ArithmeticException Nếu gặp lỗi trong các phép toán modular nghịch đảo (gcd(k, p-1) != 1)
     */
    public SignatureData kyVanBan(byte[] data, KeyPairData keyPair) throws ArithmeticException {
        if (data == null || keyPair == null || !keyPair.hasPrivateKey()) {
            throw new IllegalArgumentException("Dữ liệu ký hoặc khóa bí mật không hợp lệ.");
        }

        BigInteger p = keyPair.getP();
        BigInteger alpha = keyPair.getAlpha();
        BigInteger x = keyPair.getX();
        BigInteger pm1 = p.subtract(BigInteger.ONE);

        // 1. Băm dữ liệu bằng thuật toán SHA-256
        BigInteger m = layGiaTriBamSHA256(data);

        // 2. Chọn ngẫu nhiên k sao cho 1 < k < p-1 và gcd(k, p-1) = 1
        BigInteger k;
        BigInteger r;
        BigInteger s;

        while (true) {
            do {
                k = new BigInteger(pm1.bitLength(), random);
            } while (k.compareTo(BigInteger.ONE) <= 0 || k.compareTo(pm1) >= 0);

            // Kiểm tra tính nguyên tố cùng nhau giữa k và p-1
            if (k.gcd(pm1).equals(BigInteger.ONE)) {
                // 3. Tính r = alpha^k mod p
                r = alpha.modPow(k, p);

                // 4. Tính s = k^-1 * (m - x*r) mod (p-1)
                try {
                    BigInteger kInv = k.modInverse(pm1); // k^(-1) mod (p-1)
                    BigInteger xr = x.multiply(r).mod(pm1); // (x * r) mod (p-1)
                    
                    // (m - x * r) mod (p-1)
                    BigInteger mMinusXr = m.subtract(xr).mod(pm1);
                    
                    s = kInv.multiply(mMinusXr).mod(pm1);

                    // Tránh trường hợp s = 0 vì là trường hợp chữ ký suy biến yếu
                    if (!s.equals(BigInteger.ZERO)) {
                        break;
                    }
                } catch (ArithmeticException e) {
                    // Nếu k không có nghịch đảo modular (gcd != 1), tiếp tục lặp để chọn k khác.
                    // Điều này giải quyết yêu cầu try-catch bắt ArithmeticException khi gcd(k, p-1) != 1.
                }
            }
        }

        return new SignatureData(r, s);
    }

    /**
     * Thuật toán xác minh chữ ký số ElGamal trên mảng byte dữ liệu nhị phân.
     * 
     * @param data Dữ liệu mảng byte gốc đầu vào
     * @param signature Đối tượng chữ ký số (r, s)
     * @param p Số nguyên tố lớn p
     * @param alpha Căn nguyên thủy alpha
     * @param y Khóa công khai y
     * @return true nếu chữ ký hợp lệ và tệp tin chưa bị sửa đổi; false nếu ngược lại
     */
    public boolean xacMinhChuKy(byte[] data, SignatureData signature, BigInteger p, BigInteger alpha, BigInteger y) {
        if (data == null || signature == null || p == null || alpha == null || y == null) {
            return false;
        }

        BigInteger r = signature.getR();
        BigInteger s = signature.getS();
        BigInteger pm1 = p.subtract(BigInteger.ONE);

        // 1. Ràng buộc bảo mật bắt buộc: 0 < r < p và 0 < s < p-1
        if (r.compareTo(BigInteger.ZERO) <= 0 || r.compareTo(p) >= 0) {
            return false;
        }
        if (s.compareTo(BigInteger.ZERO) <= 0 || s.compareTo(pm1) >= 0) {
            return false;
        }

        // 2. Tính giá trị băm m = SHA-256(data)
        BigInteger m = layGiaTriBamSHA256(data);

        // 3. Tính v1 = alpha^m mod p
        BigInteger v1 = alpha.modPow(m, p);

        // 4. Tính v2 = y^r * r^s mod p
        BigInteger yr = y.modPow(r, p);
        BigInteger rs = r.modPow(s, p);
        BigInteger v2 = yr.multiply(rs).mod(p);

        // Chữ ký hợp lệ khi và chỉ khi v1 = v2
        return v1.equals(v2);
    }

    /**
     * Hàm băm phụ trợ: Chuyển đổi dữ liệu nhị phân đầu vào thành một số BigInteger dương dựa trên SHA-256.
     */
    private BigInteger layGiaTriBamSHA256(byte[] data) {
        try {
            MessageDigest md = MessageDigest.getInstance("SHA-256");
            byte[] hashBytes = md.digest(data);
            // Tham số signum = 1 để chuyển mảng byte thành BigInteger dương
            return new BigInteger(1, hashBytes);
        } catch (NoSuchAlgorithmException e) {
            // SHA-256 là thuật toán mặc định bắt buộc từ JDK 1.4 trở lên nên ngoại lệ này hiếm khi xảy ra
            throw new RuntimeException("Lỗi hệ thống: Thuật toán SHA-256 không được hỗ trợ trong môi trường này.", e);
        }
    }
}

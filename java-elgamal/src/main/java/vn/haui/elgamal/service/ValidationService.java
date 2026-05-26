package vn.haui.elgamal.service;

import vn.haui.elgamal.model.SignatureData;

import java.math.BigInteger;
import java.util.Base64;

/**
 * Lớp dịch vụ kiểm tra tính hợp lệ của dữ liệu đầu vào và phân tích cú pháp chữ ký.
 * Giúp tách biệt mã nguồn kiểm tra dữ liệu ra khỏi Controller và Model, giữ ứng dụng sạch và dễ debug.
 */
public class ValidationService {

    /**
     * Kiểm tra xem số n có phải là số nguyên tố hay không.
     * Sử dụng thuật toán Miller-Rabin với 40 vòng lặp cho độ chính xác cực cao (sai số nhỏ hơn 2^-80).
     */
    public static boolean isPrime(BigInteger n) {
        if (n == null) return false;
        // Số nguyên tố phải lớn hơn 1
        if (n.compareTo(BigInteger.ONE) <= 0) return false;
        return n.isProbablePrime(40);
    }

    /**
     * Kiểm tra xem alpha có phải là căn nguyên thủy (primitive root) modulo p hay không.
     * 
     * - Đối với số nguyên tố an toàn p = 2q + 1 (q nguyên tố): kiểm tra chính xác 100% bằng cách 
     *   đảm bảo alpha^2 != 1 mod p và alpha^q != 1 mod p.
     * - Đối với số nguyên tố thường: áp dụng kiểm tra Fermat và heuristic các ước số nguyên tố nhỏ của p-1.
     */
    public static boolean isPrimitiveRoot(BigInteger alpha, BigInteger p) {
        if (alpha == null || p == null) return false;
        
        // Ràng buộc 1 < alpha < p-1
        if (alpha.compareTo(BigInteger.ONE) <= 0 || alpha.compareTo(p.subtract(BigInteger.ONE)) >= 0) {
            return false;
        }

        // Định lý Fermat nhỏ: alpha^(p-1) mod p phải bằng 1
        BigInteger pm1 = p.subtract(BigInteger.ONE);
        if (!alpha.modPow(pm1, p).equals(BigInteger.ONE)) {
            return false;
        }

        // Kiểm tra xem p có phải là số nguyên tố an toàn (Safe Prime) không
        BigInteger q = pm1.divide(BigInteger.TWO);
        if (q.isProbablePrime(40)) {
            // Với Safe Prime, các ước của p-1 chỉ gồm 1, 2, q, 2q.
            // Do alpha > 1, bậc không thể là 1. Ta chỉ cần kiểm tra bậc khác 2 và khác q.
            if (alpha.modPow(BigInteger.TWO, p).equals(BigInteger.ONE)) {
                return false;
            }
            if (alpha.modPow(q, p).equals(BigInteger.ONE)) {
                return false;
            }
            return true;
        }

        // Trường hợp số nguyên tố thường p: Kiểm tra heuristic dựa trên tập hợp các ước số nguyên tố nhỏ của p-1.
        // Đây là phương án demo thực dụng vì không thể phân tích thừa số nguyên tố đầy đủ cho một số 512-bit trong thời gian thực.
        int[] smallPrimes = {2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97};
        for (int factor : smallPrimes) {
            BigInteger bFactor = BigInteger.valueOf(factor);
            if (pm1.mod(bFactor).equals(BigInteger.ZERO)) {
                BigInteger power = pm1.divide(bFactor);
                // Nếu tồn tại ước d sao cho alpha^((p-1)/d) = 1 mod p thì alpha không phải căn nguyên thủy.
                if (alpha.modPow(power, p).equals(BigInteger.ONE)) {
                    return false;
                }
            }
        }

        return true;
    }

    /**
     * Xác định xem việc kiểm định căn nguyên thủy cho p có phải là kiểm định heuristic (không đầy đủ) hay không.
     * Trả về true nếu p không phải là Safe Prime (tức q = (p-1)/2 không nguyên tố), giúp Controller hiển thị cảnh báo mềm.
     */
    public static boolean isHeuristicPrimitiveRootCheck(BigInteger p) {
        if (p == null) return false;
        BigInteger pm1 = p.subtract(BigInteger.ONE);
        BigInteger q = pm1.divide(BigInteger.TWO);
        return !q.isProbablePrime(40);
    }

    /**
     * Kiểm tra tính hợp lệ của khóa bí mật x.
     * Ràng buộc: 1 < x < p-1.
     */
    public static boolean isValidPrivateKey(BigInteger x, BigInteger p) {
        if (x == null || p == null) return false;
        return x.compareTo(BigInteger.ONE) > 0 && x.compareTo(p.subtract(BigInteger.ONE)) < 0;
    }

    /**
     * Phân tích cú pháp chuỗi chữ ký Base64 nhập từ người dùng.
     * Định dạng yêu cầu: r_base64|s_base64
     * 
     * @param sigStr Chuỗi chữ ký Base64 phân tách bằng ký tự '|'
     * @return Đối tượng SignatureData chứa r và s dạng BigInteger
     * @throws IllegalArgumentException Nếu dán sai định dạng chữ ký hoặc chuỗi Base64 không hợp lệ
     */
    public static SignatureData parseSignature(String sigStr) throws IllegalArgumentException {
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
            throw new IllegalArgumentException("Chuỗi mã hóa Base64 của chữ ký không hợp lệ, vui lòng kiểm tra lại.");
        } catch (Exception e) {
            throw new IllegalArgumentException("Định dạng dữ liệu số bên trong chữ ký không hợp lệ.");
        }
    }
}

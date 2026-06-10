package vn.haui.elgamal.service;

import vn.haui.elgamal.model.KeyPairData;

import java.io.File;
import java.io.IOException;
import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.List;

/**
 * Lớp dịch vụ hỗ trợ đọc và ghi tệp tin trong ứng dụng.
 * Hỗ trợ băm nhị phân các tệp phức tạp bằng cách đọc trực tiếp mảng byte.
 */
public class FileService {

    /**
     * Đọc toàn bộ dữ liệu nhị phân (mảng byte) của một tệp tin bất kỳ.
     * Sử dụng cho việc băm SHA-256 các file phức tạp như .pdf, .docx, .txt,...
     */
    public static byte[] readBytesFromFile(File file) throws IOException {
        if (file == null || !file.exists()) {
            throw new IOException("Tệp tin không tồn tại hoặc đường dẫn không hợp lệ.");
        }
        return Files.readAllBytes(file.toPath());
    }

    /**
     * Đọc nội dung văn bản thuần túy dạng chuỗi từ một tệp tin (thường là .txt hoặc .sig).
     */
    public static String readStringFromFile(File file) throws IOException {
        byte[] bytes = readBytesFromFile(file);
        return new String(bytes, StandardCharsets.UTF_8);
    }

    /**
     * Ghi nội dung văn bản thuần túy vào một tệp tin.
     */
    public static void writeStringToFile(File file, String content) throws IOException {
        if (file == null) {
            throw new IOException("Đường dẫn lưu tệp không hợp lệ.");
        }
        Files.writeString(file.toPath(), content, StandardCharsets.UTF_8);
    }

    /**
     * Lưu chữ ký số dạng Base64 (r_base64|s_base64) ra tệp tin chữ ký (.sig).
     */
    public static void writeSignatureFile(File file, String signatureStr) throws IOException {
        writeStringToFile(file, signatureStr);
    }

    /**
     * Lưu thông tin khóa ElGamal ra tệp tin .txt theo định dạng dòng cấu hình tường minh:
     * 
     * p=value
     * alpha=value
     * x=value (nếu có khóa bí mật)
     * y=value
     */
    public static void writeKeyFile(File file, KeyPairData keyPair) throws IOException {
        if (keyPair == null) {
            throw new IOException("Cặp khóa rỗng, không thể lưu.");
        }

        StringBuilder sb = new StringBuilder();
        sb.append("p=").append(keyPair.getP().toString()).append("\n");
        sb.append("alpha=").append(keyPair.getAlpha().toString()).append("\n");
        
        if (keyPair.hasPrivateKey()) {
            sb.append("x=").append(keyPair.getX().toString()).append("\n");
        }
        
        sb.append("y=").append(keyPair.getY().toString()).append("\n");

        writeStringToFile(file, sb.toString());
    }
    /**
     * Đọc thông tin khóa ElGamal từ tệp tin .txt.
     * Định dạng tệp:
     * p=<value>
     * alpha=<value>
     * x=<value> (tùy chọn)
     * y=<value>
     * 
     * @param file Tệp tin cần đọc
     * @return Đối tượng KeyPairData chứa các thành phần khóa
     * @throws IOException Nếu tệp không hợp lệ hoặc thiếu trường bắt buộc
     */
    public static KeyPairData readKeyFile(File file) throws IOException {
        if (file == null || !file.exists()) {
            throw new IOException("Tệp tin khóa không tồn tại hoặc đường dẫn không hợp lệ.");
        }

        List<String> lines = Files.readAllLines(file.toPath(), StandardCharsets.UTF_8);
        BigInteger p = null;
        BigInteger alpha = null;
        BigInteger x = null;
        BigInteger y = null;

        for (String line : lines) {
            line = line.trim();
            if (line.isEmpty() || !line.contains("=")) continue;
            
            String[] parts = line.split("=", 2);
            if (parts.length != 2) continue;
            
            String key = parts[0].trim().toLowerCase();
            String value = parts[1].trim();
            
            try {
                switch (key) {
                    case "p": p = new BigInteger(value); break;
                    case "alpha": alpha = new BigInteger(value); break;
                    case "x": x = new BigInteger(value); break;
                    case "y": y = new BigInteger(value); break;
                }
            } catch (NumberFormatException e) {
                throw new IOException("Giá trị của trường " + key + " không phải là số hợp lệ.");
            }
        }

        if (p == null || alpha == null || y == null) {
            throw new IOException("Tệp khóa không hợp lệ: thiếu trường bắt buộc p/alpha/y");
        }

        if (x != null) {
            return new KeyPairData(p, alpha, x, y);
        } else {
            return new KeyPairData(p, alpha, y);
        }
    }
}

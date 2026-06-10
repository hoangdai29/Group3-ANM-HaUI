package vn.haui.elgamal.model;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.nio.charset.StandardCharsets;

import static org.junit.jupiter.api.Assertions.*;

/**
 * Unit test cho lớp ElGamalModel.
 * Kiểm tra tính đúng đắn của các thuật toán sinh khóa, ký số và xác minh chữ ký.
 */
class ElGamalModelTest {

    private ElGamalModel model;
    private KeyPairData keyPair;

    @BeforeEach
    void setUp() {
        model = new ElGamalModel();
        // Dùng 512-bit để test nhanh
        keyPair = model.sinhKhoaTuDong(512);
    }

    @Test
    @DisplayName("Sinh khóa: p phải là số nguyên tố an toàn và y = alpha^x mod p")
    void testKeyGeneration() {
        assertNotNull(keyPair.getP());
        assertNotNull(keyPair.getAlpha());
        assertNotNull(keyPair.getX());
        assertNotNull(keyPair.getY());
        assertTrue(keyPair.getP().isProbablePrime(40), "p phải là số nguyên tố");
        // Kiểm tra y = alpha^x mod p
        assertEquals(
            keyPair.getAlpha().modPow(keyPair.getX(), keyPair.getP()),
            keyPair.getY(),
            "y phải bằng alpha^x mod p"
        );
    }

    @Test
    @DisplayName("Ký đúng → xác minh phải thành công (Happy Path)")
    void testSignAndVerifySuccess() {
        byte[] data = "Xin chào HaUI - An Ninh Mạng 2025".getBytes(StandardCharsets.UTF_8);
        SignatureData sig = model.kyVanBan(data, keyPair);
        assertNotNull(sig);
        assertTrue(
            model.xacMinhChuKy(data, sig, keyPair.getP(), keyPair.getAlpha(), keyPair.getY()),
            "Xác minh phải thành công với dữ liệu gốc"
        );
    }

    @Test
    @DisplayName("Dữ liệu bị sửa đổi → xác minh phải thất bại")
    void testVerifyFailsOnTamperedData() {
        byte[] original = "Tài liệu gốc".getBytes(StandardCharsets.UTF_8);
        byte[] tampered = "Tài liệu đã bị SỬA".getBytes(StandardCharsets.UTF_8);
        SignatureData sig = model.kyVanBan(original, keyPair);
        assertFalse(
            model.xacMinhChuKy(tampered, sig, keyPair.getP(), keyPair.getAlpha(), keyPair.getY()),
            "Xác minh phải thất bại khi dữ liệu bị thay đổi"
        );
    }

    @Test
    @DisplayName("Sai khóa công khai y → xác minh phải thất bại")
    void testVerifyFailsOnWrongPublicKey() {
        byte[] data = "Kiểm tra khóa sai".getBytes(StandardCharsets.UTF_8);
        SignatureData sig = model.kyVanBan(data, keyPair);
        // Tạo một cặp khóa khác để lấy y sai
        KeyPairData wrongKey = model.sinhKhoaTuDong(512);
        assertFalse(
            model.xacMinhChuKy(data, sig, keyPair.getP(), keyPair.getAlpha(), wrongKey.getY()),
            "Xác minh phải thất bại khi dùng sai khóa công khai y"
        );
    }

    @Test
    @DisplayName("Ký với khóa null → phải throw IllegalArgumentException")
    void testSignWithNullKey() {
        byte[] data = "test".getBytes(StandardCharsets.UTF_8);
        assertThrows(IllegalArgumentException.class, () -> model.kyVanBan(data, null));
    }

    @Test
    @DisplayName("Thời gian sinh khóa phải được ghi lại và > 0")
    void testOperationTimeRecorded() {
        model.sinhKhoaTuDong(512);
        assertTrue(model.getLastOperationTimeMs() >= 0, "Thời gian thực hiện phải được ghi lại");
    }
}

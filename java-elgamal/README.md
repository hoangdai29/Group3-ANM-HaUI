# Hệ Thống Chữ Ký Số ElGamal
Bài thực nghiệm môn An Ninh Mạng — Trường Công nghệ thông tin, ĐH Công nghiệp Hà Nội (HaUI)

## Công nghệ
* Java 17 + JavaFX 17 + Maven
* Thuật toán: ElGamal Digital Signature + SHA-256

## Khởi chạy
```bash
mvn clean javafx:run
```

## Tham số hệ thống
| Tham số | Giá trị | Mô tả |
| :--- | :--- | :--- |
| Độ dài bit | 512 / 1024 | Độ dài của số nguyên tố p |
| Hash | SHA-256 | Hàm băm dữ liệu |
| Loại p | Safe Prime | p = 2q+1, q nguyên tố |
| Miller-Rabin | 40 vòng | Độ chính xác xác suất ≥ 1 - 2^(-80) |

## Chạy Unit Test
```bash
mvn test
```

## ⚠️ Hạn chế bảo mật (Limitations)
* **512-bit không còn đủ an toàn** theo tiêu chuẩn NIST 2024 — chỉ dùng cho mục đích học tập. Thực tế cần ≥ 2048-bit.
* **Nguy cơ Chosen-Message Attack:** Nếu giá trị ngẫu nhiên k bị tái sử dụng cho hai thông điệp khác nhau, toàn bộ khóa bí mật x sẽ bị lộ. Ứng dụng dùng SecureRandom để giảm thiểu rủi ro nhưng không loại trừ hoàn toàn.
* **Không có cơ chế PKI:** Chưa có cơ chế xác thực chủ sở hữu khóa công khai — chỉ phù hợp môi trường thực nghiệm.
* **Heuristic Primitive Root Check:** Với số nguyên tố thường (không phải Safe Prime), kiểm tra căn nguyên thủy chỉ mang tính xác suất dựa trên tập ước số nhỏ.

## Tác giả
* Họ tên: [Phạm Văn Hiếu]
* MSSV: [2023603007]
* Lớp: [CNTT04]

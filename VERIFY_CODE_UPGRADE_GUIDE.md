# Hướng dẫn Nâng cấp handleVerify() - Multiple Error Handling

## Tóm tắt thay đổi

Hàm `handleVerify()` đã được nâng cấp từ cơ chế **if-else-if** (báo lỗi đầu tiên) sang cơ chế **error collection** (báo nhiều lỗi cùng lúc).

---

## 1. Cấu trúc mới

### Class VerificationError (inner static class)
```java
private static class VerificationError {
    final String title;      // Dòng tiêu đề lỗi (VD: "❌ Sai chữ ký số")
    final String detail;     // Chi tiết lỗi (VD: "Thành phần r không hợp lệ.")
    final String status;     // Trạng thái: "INVALID" hoặc "DEFAULT"
    
    VerificationError(String title, String detail, String status) { ... }
}
```

### Method displayVerificationErrors()
Xử lý hiển thị lỗi theo số lượng:
- **1 lỗi**: Hiển thị đầy đủ (title + detail)
- **2+ lỗi**: Chỉ hiển thị titles, ghép bằng `\n`, tự động dedup

---

## 2. Quy trình xác thực 5 phases

### Phase 1: Basic Input Validation
Kiểm tra input cơ bản (không throw exception)
- sigStr, yStr, pStr, alphaStr có trống?
- Nếu có lỗi → display + return

### Phase 2: Parse Signature
```java
try {
    signature = ValidationService.parseSignature(sigStr);
} catch (IllegalArgumentException e) {
    errors.add(new VerificationError(
        "❌ Chữ ký đã bị sửa đổi",
        "Cấu trúc chữ ký không toàn vẹn...",
        "INVALID"
    ));
    displayVerificationErrors(errors);
    return;
}
```

### Phase 3: Parse p, alpha, y
Chuyển đổi từ String → BigInteger, bắt NumberFormatException

### Phase 4: Validate Range
Kiểm tra giá trị r, s, y trong phạm vi hợp lệ
- r, s, y sai → add vào errors list, **KHÔNG return**
- Cho phép collect cả r và s sai cùng lúc

### Phase 5: Get Input Data
Đọc file hoặc text để xác minh, bắt I/O exceptions

---

## 3. Hành động hiển thị lỗi

### Ví dụ 1: 1 lỗi (y sai)
```
Input: y nằm ngoài khoảng [1, p-1]

Output hiển thị:
❌ Sai khóa công khai y
Giá trị y không nằm trong khoảng hợp lệ.

Status: INVALID (màu đỏ)
```

### Ví dụ 2: 2+ lỗi (r sai + s sai + y sai)
```
Input: Cả r, s, y đều outside valid ranges

Output hiển thị:
❌ Sai khóa công khai y
❌ Sai chữ ký số

Status: INVALID (màu đỏ)
```
**Lưu ý**: 
- Chỉ hiển thị 2 title (dedup "❌ Sai chữ ký số" từ r và s)
- KHÔNG hiển thị detail

### Ví dụ 3: Mixed errors (input empty + parse error)
```
Input: sigStr trống + yStr trống

Output hiển thị:
❌ Vui lòng cung cấp chữ ký số.
❌ Vui lòng cung cấp khóa công khai y.

Status: DEFAULT (màu xám)
```

---

## 4. Deduplication Logic

Tự động gộp lỗi có **title giống nhau**:

**Trước (old code - báo 2 error riêng rẽ):**
```
if (r error)     → return "❌ Sai chữ ký số\nThành phần r không hợp lệ."
if (s error)     → (không execute vì đã return)
```

**Sau (new code - báo chung):**
```
if (r error)     → add "❌ Sai chữ ký số" to errors
if (s error)     → add "❌ Sai chữ ký số" to errors
display          → show only 1 "❌ Sai chữ ký số" (dedup bằng LinkedHashSet)
```

---

## 5. Log Messages

Mỗi phase có log khi lỗi:

| Phase | Log Action | Log Message |
|-------|-----------|-------------|
| Phase 1 | LỖI XÁC MINH | "Thiếu dữ liệu đầu vào." |
| Phase 2 | XÁC MINH THẤT BẠI | "Chữ ký đã bị can thiệp/sửa đổi (Lỗi toàn vẹn cấu trúc)." |
| Phase 3-4 | (no log, continues) | |
| Phase 5 | (no log, continues) | |
| Success | XÁC MINH THÀNH CÔNG | "Chữ ký hợp lệ trên [source]." |

---

## 6. Bảo tồn nguyên văn tiếng Việt

✅ **Giữ nguyên**:
- Icon ❌, ✅
- Text tiếng Việt đầy đủ
- Không thêm câu dẫn như "Các lỗi mắc phải:"

❌ **KHÔNG được**:
- Sửa văn phong
- Bỏ icon
- Thêm introduce phrases

---

## 7. Test Case

**Test 1: r sai + s sai**
```
Action: Nhập r invalid, s invalid
Expected Output: 
❌ Sai chữ ký số
(chỉ 1 dòng, dedup)
Status: INVALID
```

**Test 2: y sai + r sai**
```
Action: Nhập y invalid, r invalid
Expected Output:
❌ Sai khóa công khai y
❌ Sai chữ ký số
(2 dòng, không dedup vì title khác)
Status: INVALID
```

**Test 3: All validation pass → verify failed at runtime**
```
Action: Pass pre-validation, nhưng sig không khớp
Flow: Vào task.setOnSucceeded → post-verification logic
Expected: Show "❌ Sai chữ ký số\nChữ ký không khớp..."
(old logic, unchanged)
```

---

## 8. Code Changes Summary

| Item | Change |
|------|--------|
| Imports | + `ArrayList`, `LinkedHashSet`, `List` |
| New Method | `displayVerificationErrors(List<VerificationError>)` |
| New Inner Class | `VerificationError` |
| Refactored Method | `handleVerify()` |
| Removed | `requireSystemParamsForVerify()` call (merged into Phase 1) |
| Preserved | `task.setOnSucceeded()` post-verification logic |

---

## 9. Backward Compatibility

✅ **100% compatible**:
- Single error behavior = exactly same as old code
- Post-verification logic = unchanged
- All error messages = unchanged
- Only difference: multiple errors now shown together

---

## Kiểm tra thêm

Nếu cần test chi tiết:
1. Mở application
2. Bước 4 (Xác minh) → Chọn "Văn bản trực tiếp"
3. Nhập test data sao cho có 2+ lỗi cùng lúc
4. Click "Xác minh" → Xem kết quả
5. So sánh với guide trên


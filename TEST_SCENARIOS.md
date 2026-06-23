# Test Scenarios - Nâng cấp handleVerify()

## Chuẩn bị Test

1. Chạy application
2. Đi tới **Bước 1 (Sinh/Nạp khóa)** → Sinh khóa 512-bit (auto gen)
3. Đi tới **Bước 4 (Xác minh)** 

---

## Test Case 1: Single Error - Empty Signature

### Setup
- **Chữ ký số** (taVerifySignature): <leave empty>
- **Khóa công khai y**: <any valid value>
- **p, alpha**: (auto filled từ Bước 1)

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌─────────────────────────────────────────┐
│ ❌ Vui lòng cung cấp chữ ký số.         │
└─────────────────────────────────────────┘
Status: DEFAULT (gray background)
```

**Lý do**: Chỉ 1 error, hiển thị title nguyên vẹn (không có detail)

---

## Test Case 2: Single Error - Invalid Y Range

### Setup
- **Chữ ký số**: nhập bất kỳ giá trị hợp lệ (VD: `ABC123|DEF456`)
- **Khóa công khai y**: `0` (outside valid range [1, p-1])
- **p, alpha**: (auto filled)

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌──────────────────────────────────────────────────────┐
│ ❌ Sai khóa công khai y                              │
│ Giá trị y không nằm trong khoảng hợp lệ.             │
└──────────────────────────────────────────────────────┘
Status: INVALID (red background)
```

**Lý do**: Chỉ 1 error, hiển thị title + detail (cách nhau bằng `\n`)

---

## Test Case 3: Two Errors - R and S Both Invalid

### Scenario
Tùy chỉnh signature sao cho parse được (format hợp lệ) nhưng r, s nằm ngoài range.

### Setup
- **Chữ ký số**: nhập `999999999999999999|999999999999999999` 
  (ngoài range của p)
- **Khóa công khai y**: <valid, in range>
- **p, alpha**: (auto filled)

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌─────────────────────────────┐
│ ❌ Sai chữ ký số            │
└─────────────────────────────┘
Status: INVALID (red background)
```

**Lý do**: 
- 2 errors trong list: r invalid, s invalid
- **Cả 2 có cùng title** "❌ Sai chữ ký số"
- LinkedHashSet dedup → chỉ hiển thị 1 lần
- Hiển thị chỉ titles, KHÔNG hiển thị details

---

## Test Case 4: Three Different Errors - Y Invalid + R Invalid + S Invalid

### Setup
- **Chữ ký số**: `999999999999|999999999999` (both r, s out of range)
- **Khóa công khai y**: `99999999999999999` (out of range)
- **p, alpha**: (auto filled)

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌──────────────────────────────────┐
│ ❌ Sai khóa công khai y          │
│ ❌ Sai chữ ký số                 │
└──────────────────────────────────┘
Status: INVALID (red background)
```

**Lý do**:
- 3 errors trong list: y invalid, r invalid, s invalid
- Titles: "❌ Sai khóa công khai y", "❌ Sai chữ ký số"
- LinkedHashSet dedup → chỉ 2 unique titles (vì r, s share same title)
- Hiển thị 2 dòng, KHÔNG hiển thị details
- KHÔNG thêm "Các lỗi mắc phải:" hay introduce phrase

---

## Test Case 5: Missing System Params (p and/or alpha)

### Setup
- **Bước 1**: Đừng sinh khóa, để trống p và alpha
- Đi tới **Bước 4**
- **Chữ ký số**: nhập bất kỳ value
- **Khóa công khai y**: nhập bất kỳ value

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌────────────────────────────────────────────────────-──────┐
│ ❌ Thiếu thông tin hệ thống p và alpha ở Bước 1.         │
└────────────────────────────────────────────────────────────┘
Status: DEFAULT (gray background)
Log: [HH:MM:SS] [LỖI XÁC MINH] Thiếu dữ liệu đầu vào.
```

**Lý do**: Phase 1 validation fail (basic input check)

---

## Test Case 6: Corrupted Signature Format

### Setup
- **Chữ ký số**: `invalid-format-not-base64` (không phải format r|s)
- **Khóa công khai y**: <valid>
- **p, alpha**: (auto filled)

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌────────────────────────────────────────────────────────────┐
│ ❌ Chữ ký đã bị sửa đổi                                    │
│ Cấu trúc chữ ký không toàn vẹn hoặc đã bị can thiệp       │
│ trên đường truyền.                                         │
└────────────────────────────────────────────────────────────┘
Status: INVALID (red background)
Log: [HH:MM:SS] [XÁC MINH THẤT BẠI] Chữ ký đã bị can thiệp/sửa đổi (Lỗi toàn vẹn cấu trúc).
```

**Lý do**: Phase 2 - parse signature throw exception

---

## Test Case 7: Valid Pre-Validation but Signature Invalid

### Setup
1. **Bước 2 (Ký)**: Ký tài liệu nào đó
   - Copy signature ra
2. **Bước 4 (Xác minh)**: 
   - **p, alpha, y**: lấy từ Bước 1 (auto filled)
   - **Chữ ký số**: paste signature từ Step 2
   - **Văn bản**: nhập TEXT KHÁC với lúc ký (modify content)

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌──────────────────────────────────────────────────────┐
│ ❌ Văn bản bị sửa đổi                                │
│ Dữ liệu xác minh không khớp dữ liệu gốc lúc ký.     │
└──────────────────────────────────────────────────────┘
Status: INVALID (red background)
Log: [HH:MM:SS] [XÁC MINH THẤT BẠI] Dữ liệu đã bị sửa đổi.
```

**Lý do**: Pre-validation pass, nhưng post-verification (task.setOnSucceeded) detect mismatch

---

## Test Case 8: Valid Signature - All OK

### Setup
1. **Bước 2 (Ký)**:
   - Nhập text: "Test-Message-123"
   - Ký → Copy signature
2. **Bước 4 (Xác minh)**:
   - **Văn bản**: "Test-Message-123" (SAME, khớp lúc ký)
   - **Chữ ký số**: paste signature
   - **p, alpha, y**: (auto filled từ Bước 1)

### Action
Click "Xác minh"

### Expected Result
```
Display:
┌────────────────────────────────────────────────┐
│ ✅ Hợp lệ                                      │
│ Chữ ký chính xác và dữ liệu chưa bị sửa đổi.  │
└────────────────────────────────────────────────┘
Status: VALID (green background)
Log: [HH:MM:SS] [XÁC MINH THÀNH CÔNG] Chữ ký hợp lệ trên văn bản trực tiếp.
```

**Lý do**: Signature hợp lệ, data match, y match → success

---

## Checklist Validation

Sau khi test, confirm:

- [ ] Test 1: Single error shows title only (no detail)
- [ ] Test 2: Single error shows title + detail
- [ ] Test 3: Multiple errors with same title → dedup (1 line only)
- [ ] Test 4: Multiple errors with diff titles → show all (2+ lines)
- [ ] Test 5: Missing params → use "Thiếu thông tin..." message
- [ ] Test 6: Corrupted sig format → use "Chữ ký đã bị sửa đổi..." message
- [ ] Test 7: Post-validation error → shows only title + detail (OLD LOGIC)
- [ ] Test 8: Valid sig → shows success message with ✅

- [ ] NO introduce phrases like "Các lỗi mắc phải:"
- [ ] Vietnamese text preserved (no spelling changes)
- [ ] Icons ❌, ✅ present
- [ ] Multi-error display is title-only (no details)

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| All errors showing details | displayVerificationErrors logic broken | Check line 751-754 |
| Dedup not working | LinkedHashSet not used | Check line 757-761 |
| Show extra text like "Problems:" | Text added in display | Remove at line 761 |
| Vietnamese corrupted | Wrong encoding | Check file charset UTF-8 |
| Old behavior persists | Code not compiled | Rebuild project (mvn clean compile) |



using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Elgamal_Signature_CS
{
    public partial class MainWindow : Window
    {
        private ElGamalKeyPair? _key;
        private ElGamalSig? _currentSignature;
        private string _currentSignedMessage = string.Empty;

        // Bối cảnh dữ liệu đã nạp để phục vụ đối chiếu toàn vẹn
        private BigInteger? _loadedQ;
        private BigInteger? _loadedA;
        private BigInteger? _loadedYA;
        private string _fileOriginalMessage = string.Empty;
        private BigInteger? _fileOriginalYA;

        // Quản lý mảng byte của tệp tin đầu vào
        private byte[]? _signFileBytes;
        private string? _signFileName;
        private byte[]? _verifyFileBytes;
        private string? _verifyFileName;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ==================== TAB 1: TẠO KHÓA ====================

        private async void BtnGenerateKeys_Click(object sender, RoutedEventArgs e)
        {
            btnGenerateKeys.IsEnabled = false;
            btnSaveKeys.IsEnabled = false;
            SetKeyStatus("⏳  HỆ THỐNG: Đang khởi tạo cặp khóa an toàn... Vui lòng đợi", "#F4A261", "#1A1200");
            SetStatus("Đang sinh khóa hệ thống...");

            int bits = cmbKeySize.SelectedIndex switch { 0 => 256, 1 => 512, 2 => 1024, _ => 512 };

            try
            {
                _key = await Task.Run(() => ElGamalCrypto.GenerateKeys(bits));
                txtQ.Text = _key.Q.ToString();
                txtA.Text = _key.A.ToString();
                txtYA.Text = _key.YA.ToString();
                txtXA.Text = _key.XA.ToString();

                SetKeyStatus($"✅  CẤU HÌNH: Khóa {bits}-bit đã sẵn sàng hoạt động", "#00FF87", "#00140A");
                txtHeaderKeyStatus.Text = $"⬤  Khóa {bits}-bit Active";
                txtHeaderKeyStatus.Foreground = BrushOf("#00FF87");
                btnSaveKeys.IsEnabled = true;
                SetStatus($"Sinh khóa {bits}-bit thành công.");
            }
            catch (Exception ex) { ShowError("Lỗi cấu trúc sinh khóa", ex.Message); }
            finally { btnGenerateKeys.IsEnabled = true; }
        }

        private void BtnApplyManual_Click(object sender, RoutedEventArgs e)
        {
            string qStr = txtQ.Text.Trim();
            string aStr = txtA.Text.Trim();
            string xaStr = txtXA.Text.Trim();

            if (string.IsNullOrEmpty(qStr) || string.IsNullOrEmpty(aStr) || string.IsNullOrEmpty(xaStr))
            { ShowWarning("Yêu cầu hệ thống: Vui lòng điền đầy đủ các tham số q, a, X_A!"); return; }

            if (!BigInteger.TryParse(qStr, out BigInteger q) ||
                !BigInteger.TryParse(aStr, out BigInteger a) ||
                !BigInteger.TryParse(xaStr, out BigInteger xa))
            { ShowError("Lỗi định dạng", "Toàn bộ các tham số đầu vào bắt buộc phải là số nguyên hợp lệ."); return; }

            if (xa <= 1 || xa >= q - 1)
            { ShowWarning("Điều kiện toán học thất bại: Khóa bí mật X_A phải thỏa mãn (1 < X_A < q - 1)."); return; }

            BigInteger ya = BigInteger.ModPow(a, xa, q);
            txtYA.Text = ya.ToString();
            _key = new ElGamalKeyPair { Q = q, A = a, XA = xa, YA = ya };

            btnSaveKeys.IsEnabled = true;
            SetKeyStatus("⚠️  CHẾ ĐỘ THỦ CÔNG — DỮ LIỆU ĐỀ BÀI ACTIVE", "#FF9F43", "#140A00");
            txtHeaderKeyStatus.Text = "⬤  Khóa Thủ Công Active";
            txtHeaderKeyStatus.Foreground = BrushOf("#FF9F43");
            SetStatus("Đã cấu hình thông số thủ công. Khóa công khai Y_A đã tự động tính.");
            MessageBox.Show("Áp dụng thông số đề bài thành công!\nGiá trị Y_A = a^X_A mod q đã được đồng bộ.", "Thành công");
        }

        private void BtnSaveKeys_Click(object sender, RoutedEventArgs e)
        {
            if (_key == null) return;
            var dlg = new SaveFileDialog { Filter = "Khóa công khai (*.pub)|*.pub", FileName = "elgamal_key" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, $"{_key.Q}\n{_key.A}\n{_key.YA}");
                    File.WriteAllText(Path.ChangeExtension(dlg.FileName, ".pri"), _key.XA.ToString());
                    SetStatus($"Hệ thống đã kết xuất tệp khóa: {Path.GetFileName(dlg.FileName)}");
                    MessageBox.Show("Kết xuất tệp khóa thành công!\n• .pub — Chứng thư khóa công khai\n• .pri — Khóa mật mã bí mật", "Thành công");
                }
                catch (Exception ex) { ShowError("Lỗi IO File", ex.Message); }
            }
        }

        // ==================== TAB 2: KÝ SỐ DỮ LIỆU ====================

        private void BtnLoadImageSign_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Định dạng Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Tất cả tệp tin (*.*)|*.*",
                Title = "Hệ thống: Chọn tệp ảnh nguồn để thực hiện ký số"
            };
            if (dlg.ShowDialog() == true)
                LoadFileForSign(dlg.FileName, isImage: true);
        }

        private void BtnLoadPdfSign_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Tài liệu văn bản PDF (*.pdf)|*.pdf|Tất cả tệp tin (*.*)|*.*",
                Title = "Hệ thống: Chọn tài liệu PDF nguồn để thực hiện ký số"
            };
            if (dlg.ShowDialog() == true)
                LoadFileForSign(dlg.FileName, isImage: false);
        }

        private void LoadFileForSign(string filePath, bool isImage)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                _signFileBytes = bytes;
                _signFileName = filePath;

                string fileName = Path.GetFileName(filePath);
                long fileSize = bytes.Length;
                string sizeStr = fileSize >= 1024 * 1024
                    ? $"{fileSize / (1024.0 * 1024.0):F2} MB"
                    : $"{fileSize / 1024.0:F1} KB";

                borderFilePreviewSign.Visibility = Visibility.Visible;
                txtFileIconSign.Text = isImage ? "🖼️" : "📋";
                txtFileNameSign.Text = fileName;
                txtFileSizeSign.Text = sizeStr;

                string hashHex = ComputeFileHashHex(bytes);
                txtFileHashSign.Visibility = Visibility.Visible;
                txtFileHashSign.Text = $"MÃ SHA-256 ĐẠI DIỆN: {hashHex}";

                txtMsgSign.Text = string.Empty; // Xóa text tĩnh khi dùng file
                SetStatus($"Đã nạp thành công tệp tin: {fileName} ({sizeStr})");
            }
            catch (Exception ex) { ShowError("Lỗi hệ thống tập tin", ex.Message); }
        }

        private async void BtnSign_Click(object sender, RoutedEventArgs e)
        {
            if (_key == null) { ShowWarning("Hành động bị từ chối: Vui lòng khởi tạo hoặc nhập thông số khóa tại Tab 1!"); return; }

            bool signingFile = _signFileBytes != null;
            string msg;
            BigInteger hash;

            if (signingFile)
            {
                using var sha = SHA256.Create();
                byte[] raw = sha.ComputeHash(_signFileBytes!);
                hash = new BigInteger(raw, isUnsigned: true, isBigEndian: false);
                msg = $"[FILE:{Path.GetFileName(_signFileName)}]";
                txtSignHash.Text = hash.ToString();
            }
            else
            {
                msg = txtMsgSign.Text.Trim();
                if (string.IsNullOrEmpty(msg)) { ShowWarning("Yêu cầu thông tin: Vui lòng nhập nội dung thông điệp chuỗi hoặc giá trị số nguyên M!"); return; }

                if (BigInteger.TryParse(msg, out BigInteger parsedNum))
                {
                    if (parsedNum >= _key.Q)
                    {
                        hash = ElGamalCrypto.HashToBigInteger(msg);
                        SetStatus("Hệ thống phát hiện M >= q, kích hoạt cơ chế băm mật mã SHA-256 tự động.");
                    }
                    else
                    {
                        hash = parsedNum; // Bài tập toán số học nhỏ
                    }
                }
                else
                {
                    hash = ElGamalCrypto.HashToBigInteger(msg); // Chuỗi chữ thông thường
                }
                txtSignHash.Text = hash.ToString();
            }

            btnSign.IsEnabled = false;
            SetStatus("Mật mã đang tính toán khối chữ ký số...");

            try
            {
                string kStr = txtKInput.Text.Trim();
                if (!string.IsNullOrEmpty(kStr) && BigInteger.TryParse(kStr, out BigInteger customK))
                {
                    BigInteger qMinus1 = _key.Q - 1;
                    if (customK <= 1 || customK >= qMinus1 || ElGamalCrypto.GCD(customK, qMinus1) != 1)
                    { ShowError("Lỗi logic tham số k", "Giá trị số ngẫu nhiên k buộc phải thỏa mãn: (1 < k < q-1) và nguyên tố cùng nhau với (q-1)!"); return; }

                    BigInteger r = BigInteger.ModPow(_key.A, customK, _key.Q);
                    BigInteger kInv = ElGamalCrypto.ModInverse(customK, qMinus1);
                    BigInteger xar = (_key.XA * r) % qMinus1;
                    BigInteger diff = (hash - xar) % qMinus1;
                    if (diff < 0) diff += qMinus1;
                    BigInteger s = (kInv * diff) % qMinus1;
                    _currentSignature = new ElGamalSig { R = r, S = s };
                }
                else
                {
                    _currentSignature = await Task.Run(() => ElGamalCrypto.SignHash(hash, _key!));
                }

                _currentSignedMessage = msg;
                txtSignR.Text = _currentSignature.R.ToString();
                txtSignS.Text = _currentSignature.S.ToString();

                string rHex = _currentSignature.R.ToString("X");
                string sHex = _currentSignature.S.ToString("X");
                string rPreview = rHex.Length > 16 ? rHex[..16] : rHex;
                string sPreview = sHex.Length > 16 ? sHex[..16] : sHex;
                txtSignatureBlock.Text = $"SIG_BLOCK[r={rPreview}…|s={sPreview}…]";

                btnSaveSignature.IsEnabled = true;
                btnTransferToVerify.IsEnabled = true;
                SetStatus("Tạo chữ ký số ElGamal thành công.");
                MessageBox.Show("✅ Kết xuất chữ ký số mật mã thành công!", "Thành công");
            }
            catch (Exception ex) { ShowError("Lỗi ký mật mã", ex.Message); }
            finally { btnSign.IsEnabled = true; }
        }

        private void BtnTransferToVerify_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSignature == null || _key == null) return;

            txtVerifyR.Text = txtSignR.Text;
            txtVerifyS.Text = txtSignS.Text;
            txtMsgVerify.Text = _currentSignedMessage.StartsWith("[FILE:") ? string.Empty : _currentSignedMessage;

            _loadedQ = _key.Q;
            _loadedA = _key.A;
            _loadedYA = _key.YA;

            // ĐỒNG BỘ ĐỂ ĐỐI CHIẾU LỖI TOÀN VẸN
            _fileOriginalYA = _key.YA;
            _fileOriginalMessage = _currentSignedMessage;

            if (_signFileBytes != null)
            {
                _verifyFileBytes = _signFileBytes;
                _verifyFileName = _signFileName;
                ShowVerifyFilePreview();
            }

            tabMain.SelectedIndex = 2; // Chuyển luồng sang Tab Xác thực công khai
            SetStatus("Dữ liệu phiên làm việc đã được ánh xạ sang Tab Xác Thực.");
        }

        private void BtnSaveSignature_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSignature == null || _key == null) return;
            var dlg = new SaveFileDialog { Filter = "Tệp tin Chữ ký số (*.sig)|*.sig", FileName = "elgamal_signature" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // Đóng gói cấu trúc tệp ký bao gồm: r, s, Y_A và nội dung bản rõ gốc phục vụ đối chứng lỗi
                    File.WriteAllText(dlg.FileName,
                        $"{_currentSignature.R}\n{_currentSignature.S}\n{_key.YA}\n{_currentSignedMessage}");
                    SetStatus($"Đã lưu tệp chữ ký mã hóa: {Path.GetFileName(dlg.FileName)}");
                    MessageBox.Show("💾 Lưu tệp chứng thư chữ ký số (.sig) thành công!", "Thành công");
                }
                catch (Exception ex) { ShowError("Lỗi hệ thống IO", ex.Message); }
            }
        }

        // ==================== TAB 3: XÁC THỰC CHỮ KÝ (QUẢN LÝ BẮT LỖI) ====================

        private void BtnLoadImageVerify_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Định dạng Ảnh (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|Tất cả tệp tin (*.*)|*.*",
                Title = "Hệ thống: Chọn tệp ảnh cần đối soát xác thực"
            };
            if (dlg.ShowDialog() == true)
                LoadFileForVerify(dlg.FileName, isImage: true);
        }

        private void BtnLoadPdfVerify_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Tài liệu văn bản PDF (*.pdf)|*.pdf|Tất cả tệp tin (*.*)|*.*",
                Title = "Hệ thống: Chọn văn bản PDF cần đối soát xác thực"
            };
            if (dlg.ShowDialog() == true)
                LoadFileForVerify(dlg.FileName, isImage: false);
        }

        private void LoadFileForVerify(string filePath, bool isImage)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                _verifyFileBytes = bytes;
                _verifyFileName = filePath;

                txtMsgVerify.Text = string.Empty;
                _fileOriginalMessage = $"[FILE:{Path.GetFileName(filePath)}]";

                string fileName = Path.GetFileName(filePath);
                long fileSize = bytes.Length;
                string sizeStr = fileSize >= 1024 * 1024
                    ? $"{fileSize / (1024.0 * 1024.0):F2} MB"
                    : $"{fileSize / 1024.0:F1} KB";

                borderFilePreviewVerify.Visibility = Visibility.Visible;
                txtFileIconVerify.Text = isImage ? "🖼️" : "📋";
                txtFileNameVerify.Text = fileName;
                txtFileSizeVerify.Text = sizeStr;

                string hashHex = ComputeFileHashHex(bytes);
                txtFileHashVerify.Visibility = Visibility.Visible;
                txtFileHashVerify.Text = $"MÃ SHA-256 ĐỐI CHIẾU: {hashHex}";

                SetStatus($"Đã nạp tệp kiểm thử chứng thực: {fileName}");
            }
            catch (Exception ex) { ShowError("Lỗi đọc luồng tệp tin", ex.Message); }
        }

        private void ShowVerifyFilePreview()
        {
            if (_verifyFileBytes == null || _verifyFileName == null) return;
            string fileName = Path.GetFileName(_verifyFileName);
            long fileSize = _verifyFileBytes.Length;
            string sizeStr = fileSize >= 1024 * 1024
                ? $"{fileSize / (1024.0 * 1024.0):F2} MB"
                : $"{fileSize / 1024.0:F1} KB";

            string ext = Path.GetExtension(_verifyFileName).ToLower();
            borderFilePreviewVerify.Visibility = Visibility.Visible;
            txtFileIconVerify.Text = (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp") ? "🖼️" : "📋";
            txtFileNameVerify.Text = fileName;
            txtFileSizeVerify.Text = sizeStr;

            string hashHex = ComputeFileHashHex(_verifyFileBytes);
            txtFileHashVerify.Visibility = Visibility.Visible;
            txtFileHashVerify.Text = $"MÃ SHA-256 ĐỐI CHIẾU: {hashHex}";
        }

        private void BtnLoadPublicKey_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Tệp khóa công khai (*.pub)|*.pub|Tất cả tệp tin (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(dlg.FileName);
                    if (lines.Length >= 3 &&
                        BigInteger.TryParse(lines[0], out BigInteger q) &&
                        BigInteger.TryParse(lines[1], out BigInteger a) &&
                        BigInteger.TryParse(lines[2], out BigInteger ya))
                    {
                        _loadedQ = q; _loadedA = a; _loadedYA = ya;
                        SetStatus($"Đã tích hợp chứng thư khóa công khai: {Path.GetFileName(dlg.FileName)}");
                        MessageBox.Show("✅ Nạp tệp khóa công khai thành công!", "Thành công");
                    }
                    else { ShowError("Lỗi cấu trúc khóa", "Định dạng tệp tin khóa .pub bị hư hại hoặc không đúng tiêu chuẩn mã hóa."); }
                }
                catch (Exception ex) { ShowError("Lỗi hệ thống", ex.Message); }
            }
        }

        private void BtnLoadSignatureFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Tệp chứa chữ ký số (*.sig)|*.sig|Tất cả tệp tin (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(dlg.FileName);
                    if (lines.Length >= 4 &&
                        BigInteger.TryParse(lines[0], out BigInteger r) &&
                        BigInteger.TryParse(lines[1], out BigInteger s) &&
                        BigInteger.TryParse(lines[2], out BigInteger origYA))
                    {
                        txtVerifyR.Text = r.ToString();
                        txtVerifyS.Text = s.ToString();

                        // ĐÓNG GÓI DỮ LIỆU GỐC TỪ FILE ĐỂ THỰC HIỆN ĐỐI CHIẾU
                        _fileOriginalYA = origYA;
                        _fileOriginalMessage = string.Join(Environment.NewLine, lines, 3, lines.Length - 3).Trim();
                        txtMsgVerify.Text = _fileOriginalMessage.StartsWith("[FILE:") ? string.Empty : _fileOriginalMessage;

                        SetStatus($"Đã nạp tệp văn bản ký: {Path.GetFileName(dlg.FileName)}");
                        MessageBox.Show("✅ Nạp tệp cấu trúc chữ ký thành công!\nDữ liệu gốc đã được lưu vết tự động để phân tích lỗi toàn vẹn.", "Thành công");
                    }
                    else { ShowError("Lỗi cấu trúc dữ liệu", "Tệp tin chữ ký .sig không tuân thủ định dạng kết xuất mật mã hệ thống."); }
                }
                catch (Exception ex) { ShowError("Lỗi luồng vào", ex.Message); }
            }
        }

        private async void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            string rStr = txtVerifyR.Text.Trim();
            string sStr = txtVerifyS.Text.Trim();

            if (string.IsNullOrEmpty(rStr) || string.IsNullOrEmpty(sStr))
            { ShowWarning("Yêu cầu toán học: Vui lòng nhập giá trị hai thành phần ký số (r) và (s)!"); return; }

            if (_loadedQ == null || _loadedA == null || _loadedYA == null)
            { ShowWarning("Yêu cầu hệ thống: Vui lòng nạp chứng thư khóa công khai (.pub) trước khi thực hiện hành động này!"); return; }

            bool verifyingFile = _verifyFileBytes != null;
            BigInteger hash;
            string msgInput;

            if (verifyingFile)
            {
                msgInput = $"[FILE:{Path.GetFileName(_verifyFileName)}]";
                using var sha = SHA256.Create();
                byte[] raw = sha.ComputeHash(_verifyFileBytes!);
                hash = new BigInteger(raw, isUnsigned: true, isBigEndian: false);
            }
            else
            {
                msgInput = txtMsgVerify.Text.Trim();
                if (string.IsNullOrEmpty(msgInput)) { ShowWarning("Hành động bị hủy: Hãy nhập thông điệp chuỗi/số hoặc tải tệp tin cần đối soát!"); return; }

                if (BigInteger.TryParse(msgInput, out BigInteger parsedNum) && parsedNum < _loadedQ.Value)
                {
                    hash = parsedNum;
                }
                else
                {
                    hash = ElGamalCrypto.HashToBigInteger(msgInput);
                }
            }

            btnVerify.IsEnabled = false;
            SetResult("⏳", "— ĐANG TIẾN HÀNH XỬ LÝ TOÁN HỌC ĐỐI CHIẾU —", "#4A6070", "#08111A", "#1A3040");
            SetStatus("Đang chạy giải thuật kiểm chứng vế phương trình...");

            try
            {
                BigInteger r = BigInteger.Parse(rStr);
                BigInteger s = BigInteger.Parse(sStr);
                var sig = new ElGamalSig { R = r, S = s };
                var keyCtx = new ElGamalKeyPair { Q = _loadedQ.Value, A = _loadedA.Value, YA = _loadedYA.Value };

                var result = await Task.Run(() => ElGamalCrypto.VerifyWithHash(hash, sig, keyCtx));

                txtVHash.Text = "M = " + hash.ToString();
                txtVLHS.Text = "V₁ = " + result.lhs.ToString();
                txtVRHS.Text = "V₂ = " + result.rhs.ToString();

                // ==================== MÔ-ĐUN PHÂN TÍCH VÀ KIỂM SOÁT BẮT LỖI CHÍNH XÁC ====================

                // 1. Kiểm tra sự thay đổi của nội dung thông điệp
                bool isMessageModified = !string.IsNullOrEmpty(_fileOriginalMessage) && msgInput != _fileOriginalMessage;

                // 2. Kiểm tra sự sai lệch của cặp khóa mật mã dùng để verify
                bool isKeyIncorrect = _fileOriginalYA != null && _loadedYA.Value != _fileOriginalYA.Value;

                // 3. Kết quả xác thực toán học ElGamal cơ bản
                bool isMathValid = result.valid;

                if (isMathValid && !isMessageModified && !isKeyIncorrect)
                {
                    // KHỚP HOÀN TOÀN: TOÀN VẸN TUYỆT ĐỐI
                    SetResult("✅", "CHỮ KÝ HỢP LỆ\nThông điệp toàn vẹn, nguồn gốc thực thể được xác thực thành công!", "#00FF87", "#00140A", "#00FF87");
                    SetStatus("Xác thực chứng thư: HỢP LỆ ✅");
                }
                else
                {
                    // PHÂN TÍCH CHÍNH XÁC NGUYÊN NHÂN LỖI THEO 3 TRƯỜNG HỢP YÊU CẦU
                    string errorHeader = "❌ CHỮ KÝ KHÔNG HỢP LỆ ❌\n";
                    string errorDetail = string.Empty;

                    // TRƯỜNG HỢP SAI CẢ HAI: Thông điệp bị can thiệp ĐỒNG THỜI toán học chữ ký hoặc khóa bị hỏng
                    if (isMessageModified && (!isMathValid || isKeyIncorrect))
                    {
                        errorDetail = "VI PHẠM HỆ THỐNG PHÁT HIỆN LỖI KÉP:\n1. Nội dung thông điệp văn bản (M) đã bị thay đổi, chỉnh sửa so với bản gốc.\n2. Cấu trúc chữ ký số (r, s) hoặc Khóa công khai nạp vào không trùng khớp.";
                    }
                    // TRƯỜNG HỢP 1 SAI THÔNG ĐIỆP: Chữ ký toán học đúng thực thể nhưng thông điệp truyền tải hiện tại bị sửa đổi
                    else if (isMessageModified)
                    {
                        errorDetail = "VI PHẠM TÍNH TOÀN VẸN DỮ LIỆU:\nChữ ký mã hóa toán học là chính xác của thực thể gửi, nhưng nội dung thông điệp (M) hiện tại đã bị sửa đổi hoặc chèn mã giả mạo!";
                    }
                    // TRƯỜNG HỢP 2 SAI CHỮ KÝ/KHÓA: Thông điệp giữ nguyên bản gốc nhưng chữ ký số hoặc khóa nạp vào bị sai lệch
                    else
                    {
                        if (isKeyIncorrect)
                        {
                            errorDetail = "VI PHẠM NGUỒN GỐC CHỨNG THƯ:\nNội dung thông điệp chính xác, nhưng Khóa công khai (.pub) dùng để verify không phải là khóa đã dùng để ký tệp tin này.";
                        }
                        else
                        {
                            errorDetail = "VI PHẠM XÁC THỰC TOÁN HỌC:\nThông điệp toàn vẹn nhưng cặp chữ ký điện tử (r, s) truyền vào bị sai lệch giá trị, không thỏa mãn phương trình đồng dư V₁ ≡ V₂ mod q.";
                        }
                    }

                    // Xuất cảnh báo tương phản cao lên giao diện bảng màu đỏ rực đặc trưng
                    SetResult("❌", errorHeader + errorDetail, "#FF4D4D", "#1A0505", "#FF4D4D");
                    SetStatus("Xác thực chứng thư: THẤT BẠI ❌");
                }
            }
            catch (Exception ex)
            {
                ShowError("Lỗi thực thi toán học", ex.Message);
                SetResult("❌", "LỖI HỆ THỐNG MẬT MÃ:\nDữ liệu chuỗi số học nhập vào vượt quá giới hạn Modulus hoặc không đúng định dạng.", "#FF4D4D", "#1A0505", "#FF4D4D");
            }
            finally { btnVerify.IsEnabled = true; }
        }

        // ==================== CÁC PHƯƠNG THỨC TRỢ NĂNG (HELPERS) ====================

        private static string ComputeFileHashHex(byte[] bytes)
        {
            using var sha = SHA256.Create();
            byte[] hashBytes = sha.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private void SetKeyStatus(string text, string fg, string bg)
        {
            txtKeyStatus.Text = text;
            txtKeyStatus.Foreground = BrushOf(fg);
            borderKeyStatus.Background = BrushOf(bg);
            borderKeyStatus.BorderBrush = BrushOf(fg);
        }

        private void SetResult(string icon, string text, string fg, string bg, string border)
        {
            txtResultIcon.Text = icon;
            txtResult.Text = text;
            txtResult.Foreground = BrushOf(fg);
            borderResult.Background = BrushOf(bg);
            borderResult.BorderBrush = BrushOf(border);
        }

        private void SetStatus(string msg)
        {
            txtStatusBar.Text = msg;
        }

        private static SolidColorBrush BrushOf(string hex)
        {
            if (hex.Length == 9) hex = hex[..7];
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }

        private static void ShowWarning(string msg) =>
            MessageBox.Show(msg, "Cảnh báo hệ thống", MessageBoxButton.OK, MessageBoxImage.Warning);

        private static void ShowError(string title, string msg) =>
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private BigInteger? _originalHash;
        private BigInteger? _fileOriginalYA;

        private bool _hasTransferredContext = false;

        // Quản lý mảng byte của tệp tin đầu vào
        private byte[]? _signFileBytes;
        private string? _signFileName;
        private byte[]? _verifyFileBytes;
        private string? _verifyFileName;

        public MainWindow()
        {
            InitializeComponent();
        }

        // ==================== CỤM ĐIỀU HƯỚNG CỬA SỔ (WINDOW CONTROLS) ====================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                BtnMaximize_Click(sender, e);
            else
                this.DragMove();
        }

        private void BtnLengthCheck(object sender, RoutedEventArgs e) { }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                btnMaximize.Content = "🗖";
                btnMaximize.ToolTip = "Phóng to";
            }
            else
            {
                this.MaxHeight = SystemParameters.MaximizedPrimaryScreenHeight;
                this.MaxWidth = SystemParameters.MaximizedPrimaryScreenWidth;
                this.WindowState = WindowState.Maximized;
                btnMaximize.Content = "🗗";
                btnMaximize.ToolTip = "Thu nhỏ lại";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        // ==================== TẠO KHÓA ====================

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

        // --- CÁC HÀM VALIDATE TRỰC TIẾP CHO TỪNG Ô ---

        private void ShowInlineError(TextBlock errBlock, TextBox txtBox, string msg)
        {
            errBlock.Text = "❌ " + msg;
            errBlock.Visibility = Visibility.Visible;
            txtBox.BorderBrush = BrushOf("#FF4D4D");
        }

        private void ClearInlineError(TextBlock errBlock, TextBox txtBox, string defaultColor)
        {
            errBlock.Visibility = Visibility.Collapsed;
            txtBox.BorderBrush = BrushOf(defaultColor);
        }

        private void Txt_ClearError(object sender, TextChangedEventArgs e)
        {
            if (sender == txtQ) ClearInlineError(errQ, txtQ, "#00A2FF");
            if (sender == txtA) ClearInlineError(errA, txtA, "#00A2FF");
            if (sender == txtXA) ClearInlineError(errXA, txtXA, "#FF9F43");
        }

        private void TxtQ_LostFocus(object sender, RoutedEventArgs e) => ValidateQ();
        private void TxtA_LostFocus(object sender, RoutedEventArgs e) => ValidateA();
        private void TxtXA_LostFocus(object sender, RoutedEventArgs e) => ValidateXA();

        private bool ValidateQ()
        {
            string qStr = txtQ.Text.Trim();
            if (string.IsNullOrEmpty(qStr))
            {
                ShowInlineError(errQ, txtQ, "Bắt buộc phải nhập giá trị q.");
                return false;
            }
            if (!BigInteger.TryParse(qStr, out BigInteger q))
            {
                ShowInlineError(errQ, txtQ, "q phải là số nguyên hợp lệ.");
                return false;
            }
            if (q < 2)
            {
                ShowInlineError(errQ, txtQ, "q phải là số nguyên tố lớn hơn 1.");
                return false;
            }
            if (!ElGamalCrypto.MillerRabin(q, 20))
            {
                ShowInlineError(errQ, txtQ, $"q = {q} không phải là số nguyên tố!");
                return false;
            }

            ClearInlineError(errQ, txtQ, "#00A2FF");

            if (!string.IsNullOrWhiteSpace(txtA.Text)) ValidateA();
            if (!string.IsNullOrWhiteSpace(txtXA.Text)) ValidateXA();

            return true;
        }

        private bool ValidateA()
        {
            string aStr = txtA.Text.Trim();
            if (string.IsNullOrEmpty(aStr))
            {
                ShowInlineError(errA, txtA, "Bắt buộc phải nhập giá trị a.");
                return false;
            }
            if (!BigInteger.TryParse(aStr, out BigInteger a))
            {
                ShowInlineError(errA, txtA, "a phải là số nguyên hợp lệ.");
                return false;
            }

            if (!BigInteger.TryParse(txtQ.Text.Trim(), out BigInteger q) || !ElGamalCrypto.MillerRabin(q, 20))
            {
                ShowInlineError(errA, txtA, "Vui lòng nhập q hợp lệ trước khi xác định a.");
                return false;
            }

            if (a < 2 || a >= q)
            {
                ShowInlineError(errA, txtA, $"a phải thỏa mãn: 2 ≤ a < q (với q = {q}).");
                return false;
            }

            if (a == 1)
            {
                ShowInlineError(errA, txtA, "a = 1 không hợp lệ, đây là phần tử tầm thường (trivial element).");
                return false;
            }
            if (BigInteger.ModPow(a, q - 1, q) != 1)
            {
                ShowInlineError(errA, txtA, $"a = {a} không thỏa mãn điều kiện Fermat nhỏ (a^(q-1) mod q phải = 1).");
                return false;
            }

            BigInteger halfOrder = (q - 1) / 2;
            if (halfOrder > 1 && BigInteger.ModPow(a, halfOrder, q) == 1)
            {
                ShowInlineError(errA, txtA, $"a = {a} có bậc quá nhỏ (a^((q-1)/2) mod q = 1), không phải căn nguyên thủy tốt.");
                return false;
            }

            ClearInlineError(errA, txtA, "#00A2FF");
            return true;
        }

        private bool ValidateXA()
        {
            string xaStr = txtXA.Text.Trim();
            if (string.IsNullOrEmpty(xaStr))
            {
                ShowInlineError(errXA, txtXA, "Bắt buộc phải nhập giá trị X_A.");
                return false;
            }
            if (!BigInteger.TryParse(xaStr, out BigInteger xa))
            {
                ShowInlineError(errXA, txtXA, "X_A phải là số nguyên hợp lệ.");
                return false;
            }

            if (!BigInteger.TryParse(txtQ.Text.Trim(), out BigInteger q) || !ElGamalCrypto.MillerRabin(q, 20))
            {
                ShowInlineError(errXA, txtXA, "Vui lòng nhập q hợp lệ trước khi xác định X_A.");
                return false;
            }

            BigInteger qMinus1 = q - 1;
            if (xa <= 1 || xa >= qMinus1)
            {
                ShowInlineError(errXA, txtXA, $"X_A phải thỏa mãn: 1 < X_A < q-1 (với q-1 = {qMinus1}).");
                return false;
            }

            ClearInlineError(errXA, txtXA, "#FF9F43");
            return true;
        }

        private void BtnApplyManual_Click(object sender, RoutedEventArgs e)
        {
            bool isQValid = ValidateQ();
            bool isAValid = ValidateA();
            bool isXAValid = ValidateXA();

            if (!isQValid || !isAValid || !isXAValid)
            {
                ShowWarning("Dữ liệu nhập vào chưa hợp lệ. Vui lòng kiểm tra lại các thông báo lỗi màu đỏ!");
                return;
            }

            BigInteger q = BigInteger.Parse(txtQ.Text.Trim());
            BigInteger a = BigInteger.Parse(txtA.Text.Trim());
            BigInteger xa = BigInteger.Parse(txtXA.Text.Trim());

            BigInteger ya = BigInteger.ModPow(a, xa, q);
            txtYA.Text = ya.ToString();
            _key = new ElGamalKeyPair { Q = q, A = a, XA = xa, YA = ya };

            btnSaveKeys.IsEnabled = true;
            SetKeyStatus("⚠️  CHẾ ĐỘ THỦ CÔNG — DỮ LIỆU ĐỀ BÀI ACTIVE", "#FF9F43", "#140A00");
            txtHeaderKeyStatus.Text = "⬤  Khóa Thủ Công Active";
            txtHeaderKeyStatus.Foreground = BrushOf("#FF9F43");
            SetStatus("Đã cấu hình thông số thủ công. Khóa công khai Y_A đã tự động tính.");

            MessageBox.Show($"Áp dụng thông số đề bài thành công!\n\n• q = {q}\n• a = {a}\n• X_A = {xa}\n• Y_A = a^X_A mod q = {ya}", "Thành công");
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

                txtMsgSign.Text = string.Empty;
                SetStatus($"Đã nạp thành công tệp tin: {fileName} ({sizeStr})");
            }
            catch (Exception ex) { ShowError("Lỗi hệ thống tập tin", ex.Message); }
        }

        private async void BtnSign_Click(object sender, RoutedEventArgs e)
        {
            if (_key == null)
            {
                ShowWarning("Hành động bị từ chối: Vui lòng khởi tạo hoặc nhập thông số khóa tại Tab 1 trước khi ký số!");
                return;
            }

            if (!string.IsNullOrEmpty(txtMsgSign.Text.Trim()))
            {
                _signFileBytes = null;
                _signFileName = null;
                borderFilePreviewSign.Visibility = Visibility.Collapsed;
                txtFileHashSign.Visibility = Visibility.Collapsed;
            }

            bool signingFile = _signFileBytes != null;
            string rawMsg;
            BigInteger hash;

            if (signingFile)
            {
                using var sha = SHA256.Create();
                byte[] raw = sha.ComputeHash(_signFileBytes!);
                hash = new BigInteger(raw, isUnsigned: true, isBigEndian: false);
                rawMsg = $"[FILE:{Path.GetFileName(_signFileName)}]";
                txtSignHash.Text = hash.ToString();
            }
            else
            {
                rawMsg = txtMsgSign.Text;
                string trimmedMsg = rawMsg.Trim();

                if (string.IsNullOrEmpty(trimmedMsg))
                {
                    ShowWarning("Yêu cầu thông tin: Vui lòng nhập nội dung thông điệp hoặc tải tệp tin (ảnh/PDF) trước khi thực hiện ký số!");
                    return;
                }

                if (BigInteger.TryParse(trimmedMsg, out BigInteger parsedNum))
                {
                    if (parsedNum < 0)
                    {
                        ShowError("Lỗi giá trị M", "Thông điệp M phải là số nguyên không âm.");
                        return;
                    }

                    if (parsedNum >= _key.Q)
                    {
                        hash = ElGamalCrypto.HashToBigInteger(rawMsg);
                        SetStatus("Hệ thống phát hiện M >= q, kích hoạt cơ chế băm mật mã SHA-256 tự động.");
                    }
                    else
                    {
                        hash = parsedNum;
                    }
                }
                else
                {
                    hash = ElGamalCrypto.HashToBigInteger(rawMsg);
                }
                txtSignHash.Text = hash.ToString();
            }

            string kStr = txtKInput.Text.Trim();
            BigInteger? customK = null;

            if (!string.IsNullOrEmpty(kStr))
            {
                if (!BigInteger.TryParse(kStr, out BigInteger parsedK))
                {
                    ShowError("Lỗi định dạng k", "Giá trị k phải là một số nguyên hợp lệ.");
                    return;
                }

                BigInteger qMinus1 = _key.Q - 1;

                if (parsedK <= 1)
                {
                    ShowError("Lỗi giá trị k", $"Giá trị k = {parsedK} quá nhỏ.\nĐiều kiện bắt buộc: k > 1.");
                    return;
                }
                if (parsedK >= qMinus1)
                {
                    ShowError("Lỗi giá trị k", $"Giá trị k = {parsedK} vượt giới hạn.\nĐiều kiện bắt buộc: k < q-1 = {qMinus1}.");
                    return;
                }
                if (ElGamalCrypto.GCD(parsedK, qMinus1) != 1)
                {
                    ShowError("Lỗi giá trị k", $"Giá trị k = {parsedK} không nguyên tố cùng nhau với (q-1) = {qMinus1}.\nGCD(k, q-1) phải bằng 1.");
                    return;
                }

                customK = parsedK;
            }

            btnSign.IsEnabled = false;
            SetStatus("Mật mã đang tính toán khối chữ ký số...");

            try
            {
                if (customK.HasValue)
                {
                    BigInteger k = customK.Value;
                    BigInteger qMinus1 = _key.Q - 1;
                    BigInteger r = BigInteger.ModPow(_key.A, k, _key.Q);
                    BigInteger kInv = ElGamalCrypto.ModInverse(k, qMinus1);
                    BigInteger xar = (_key.XA * r) % qMinus1;
                    BigInteger diff = (hash - xar) % qMinus1;
                    if (diff < 0) diff += qMinus1;
                    BigInteger s = (kInv * diff) % qMinus1;

                    if (s == 0)
                    {
                        ShowError("Lỗi ký số", $"Giá trị k = {k} tạo ra s = 0, chữ ký không hợp lệ.\nVui lòng chọn một giá trị k khác.");
                        return;
                    }

                    _currentSignature = new ElGamalSig { R = r, S = s };
                }
                else
                {
                    _currentSignature = await Task.Run(() => ElGamalCrypto.SignHash(hash, _key!));
                }

                _currentSignedMessage = rawMsg;
                signingHashCache = hash;
                _originalHash = hash;

                txtSignR.Text = _currentSignature.R.ToString();
                txtSignS.Text = _currentSignature.S.ToString();

                // Cập nhật xuất ra chuỗi Token Base64 đẹp mắt
                string rBase64 = Convert.ToBase64String(_currentSignature.R.ToByteArray());
                string sBase64 = Convert.ToBase64String(_currentSignature.S.ToByteArray());
                txtSignatureBlock.Text = $"{rBase64}.{sBase64}";

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

            bool isFileSign = _currentSignedMessage.StartsWith("[FILE:");
            txtMsgVerify.Text = isFileSign ? string.Empty : _currentSignedMessage;

            _loadedQ = _key.Q;
            _loadedA = _key.A;
            _loadedYA = _key.YA;
            _fileOriginalYA = _key.YA;
            _originalHash = signingHashCache;
            _hasTransferredContext = true;

            if (_signFileBytes != null)
            {
                _verifyFileBytes = _signFileBytes;
                _verifyFileName = _signFileName;
                ShowVerifyFilePreview();
            }
            else
            {
                _verifyFileBytes = null;
                _verifyFileName = null;
                borderFilePreviewVerify.Visibility = Visibility.Collapsed;
                txtFileHashVerify.Visibility = Visibility.Collapsed;
            }

            tabMain.SelectedIndex = 2;
            SetStatus("Dữ liệu phiên làm việc đã được ánh xạ sang Tab Xác Thực.");
        }

        private BigInteger signingHashCache;

        private void BtnSaveSignature_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSignature == null || _key == null) return;
            var dlg = new SaveFileDialog { Filter = "Tệp tin Chữ ký số (*.sig)|*.sig", FileName = "elgamal_signature" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName,
                        $"{_currentSignature.R}\n{_currentSignature.S}\n{_key.YA}\n{_key.Q}\n{_key.A}\n{_currentSignedMessage}");
                    SetStatus($"Đã lưu tệp chữ ký mã hóa: {Path.GetFileName(dlg.FileName)}");
                    MessageBox.Show("💾 Lưu tệp chứng thư chữ ký số (.sig) thành công!", "Thành công");
                }
                catch (Exception ex) { ShowError("Lỗi hệ thống IO", ex.Message); }
            }
        }

        // ==================== TAB 3: XÁC THỰC CHỮ KÝ ====================

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

                _hasTransferredContext = false;
                _originalHash = null;
                txtMsgVerify.Text = string.Empty;

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
                        _hasTransferredContext = false;
                        _originalHash = null;
                        SetStatus($"Đã tích hợp chứng thư khóa công khai: {Path.GetFileName(dlg.FileName)}");
                        MessageBox.Show("✅ Nạp tệp khóa công khai thành công!", "Thành công");
                    }
                    else
                    {
                        ShowError("Lỗi cấu trúc khóa", "Định dạng tệp tin khóa .pub bị hư hại hoặc không đúng tiêu chuẩn.");
                    }
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
                    if (lines.Length >= 6 &&
                        BigInteger.TryParse(lines[0], out BigInteger r) &&
                        BigInteger.TryParse(lines[1], out BigInteger s) &&
                        BigInteger.TryParse(lines[2], out BigInteger origYA) &&
                        BigInteger.TryParse(lines[3], out BigInteger q) &&
                        BigInteger.TryParse(lines[4], out BigInteger a))
                    {
                        txtVerifyR.Text = r.ToString();
                        txtVerifyS.Text = s.ToString();

                        _loadedQ = q;
                        _loadedA = a;
                        _loadedYA = origYA;
                        _fileOriginalYA = origYA;

                        string rawStoredMsg = string.Join(Environment.NewLine, lines, 5, lines.Length - 5);
                        bool isFileMsg = rawStoredMsg.TrimStart().StartsWith("[FILE:");
                        txtMsgVerify.Text = isFileMsg ? string.Empty : rawStoredMsg;

                        if (!isFileMsg)
                        {
                            if (BigInteger.TryParse(rawStoredMsg.Trim(), out BigInteger parsedNum) && parsedNum < q)
                                _originalHash = parsedNum;
                            else
                                _originalHash = ElGamalCrypto.HashToBigInteger(rawStoredMsg);
                        }
                        else
                        {
                            _originalHash = null;
                        }

                        _hasTransferredContext = false;

                        SetStatus($"Đã nạp tệp văn bản ký: {Path.GetFileName(dlg.FileName)}");
                        MessageBox.Show("✅ Nạp tệp cấu trúc chữ ký thành công!\nCác tham số q, a và Y_A tự động đồng bộ hóa.", "Thành công");
                    }
                    else
                    {
                        ShowError("Lỗi cấu trúc dữ liệu", "Tệp tin chữ ký .sig không tuân thủ cấu trúc đồng bộ mật mã.");
                    }
                }
                catch (Exception ex) { ShowError("Lỗi luồng vào", ex.Message); }
            }
        }

        private async void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            string rStr = txtVerifyR.Text.Trim();
            string sStr = txtVerifyS.Text.Trim();

            if (_loadedQ == null || _loadedA == null || _loadedYA == null)
            {
                ShowWarning("Thiếu thông tin khóa công khai!\n\nVui lòng thực hiện một trong hai cách:\n• Nạp tệp chứng thư khóa công khai (.pub)\n• Nạp tệp chữ ký (.sig) có chứa thông tin khóa\n• Hoặc điều hướng từ Tab Ký Số sang Tab này");
                return;
            }

            if (string.IsNullOrEmpty(rStr) || string.IsNullOrEmpty(sStr))
            {
                ShowWarning("Vui lòng nhập đầy đủ cả hai thành phần chữ ký:\n• Thành phần r\n• Thành phần s");
                return;
            }

            if (!BigInteger.TryParse(rStr, out BigInteger r))
            {
                ShowError("Lỗi định dạng r", $"Giá trị r = \"{rStr}\" không phải là số nguyên hợp lệ.");
                return;
            }
            if (!BigInteger.TryParse(sStr, out BigInteger s))
            {
                ShowError("Lỗi định dạng s", $"Giá trị s = \"{sStr}\" không phải là số nguyên hợp lệ.");
                return;
            }

            BigInteger q = _loadedQ.Value;
            BigInteger qMinus1 = q - 1;

            if (r <= 0 || r >= q)
            {
                ShowError("Lỗi phạm vi r", $"Giá trị r = {r} vi phạm điều kiện: 0 < r < q.\nGiới hạn: q = {q}");
                return;
            }
            if (s <= 0 || s >= qMinus1)
            {
                ShowError("Lỗi phạm vi s", $"Giá trị s = {s} vi phạm điều kiện: 0 < s < q-1.\nGiới hạn: q-1 = {qMinus1}");
                return;
            }

            if (!string.IsNullOrEmpty(txtMsgVerify.Text.Trim()) && _verifyFileBytes != null)
            {
                _verifyFileBytes = null;
                _verifyFileName = null;
                borderFilePreviewVerify.Visibility = Visibility.Collapsed;
                txtFileHashVerify.Visibility = Visibility.Collapsed;
            }

            bool verifyingFile = _verifyFileBytes != null;
            BigInteger hash;

            if (verifyingFile)
            {
                using var sha = SHA256.Create();
                byte[] raw = sha.ComputeHash(_verifyFileBytes!);
                hash = new BigInteger(raw, isUnsigned: true, isBigEndian: false);
            }
            else
            {
                string rawVerifyMsg = txtMsgVerify.Text;

                if (string.IsNullOrEmpty(rawVerifyMsg))
                {
                    ShowWarning("Thiếu dữ liệu xác thực!\nVui lòng nhập thông điệp hoặc tải tệp tin cần kiểm tra.");
                    return;
                }

                string trimmedVerifyMsg = rawVerifyMsg.Trim();

                if (BigInteger.TryParse(trimmedVerifyMsg, out BigInteger parsedNum) && parsedNum < q)
                    hash = parsedNum;
                else
                    hash = ElGamalCrypto.HashToBigInteger(rawVerifyMsg);
            }

            btnVerify.IsEnabled = false;
            SetResult("⏳", "— ĐANG TIẾN HÀNH XỬ LÝ TOÁN HỌC ĐỐI CHIẾU —", "#4A6070", "#08111A", "#1A3040");

            try
            {
                var sig = new ElGamalSig { R = r, S = s };

                var keyCtxCurrent = new ElGamalKeyPair { Q = q, A = _loadedA.Value, YA = _loadedYA.Value };
                var resultCurrent = await Task.Run(() => ElGamalCrypto.VerifyWithHash(hash, sig, keyCtxCurrent));

                txtVHash.Text = "M = " + hash.ToString();
                txtVLHS.Text = "V₁ = " + resultCurrent.lhs.ToString();
                txtVRHS.Text = "V₂ = " + resultCurrent.rhs.ToString();

                bool mathValidWithCurrentInputs = resultCurrent.valid;

                if (mathValidWithCurrentInputs)
                {
                    if (_fileOriginalYA != null && _loadedYA.Value != _fileOriginalYA.Value)
                    {
                        SetResult("⚠️", "LỖI: Khóa bị sửa đổi hoặc sai!\n(Chữ ký khớp với khóa hiện tại nhưng khóa này đã bị tráo so với gốc)", "#FF9F43", "#1A0A00", "#FF9F43");
                    }
                    else
                    {
                        SetResult("✅", "✅ CHỮ KÝ SỐ HỢP LỆ\n\nVăn bản toàn vẹn và nguồn gốc khóa chính xác.", "#00FF87", "#00140A", "#00FF87");
                    }
                }
                else
                {
                    bool msgChanged = false;
                    bool keyChanged = false;

                    if (_fileOriginalYA.HasValue && _loadedYA.Value != _fileOriginalYA.Value)
                    {
                        keyChanged = true;
                    }

                    if (_originalHash.HasValue)
                    {
                        if (hash != _originalHash.Value) msgChanged = true;
                    }
                    else if (_fileOriginalYA.HasValue)
                    {
                        var keyCtxOrig = new ElGamalKeyPair { Q = q, A = _loadedA.Value, YA = _fileOriginalYA.Value };
                        var resultOrig = await Task.Run(() => ElGamalCrypto.VerifyWithHash(hash, sig, keyCtxOrig));
                        if (!resultOrig.valid) msgChanged = true;
                    }
                    else
                    {
                        msgChanged = true;
                    }

                    // Đã áp dụng lỗi gom nhóm theo chữ HOẶC
                    if (msgChanged && keyChanged)
                    {
                        SetResult("❌", "LỖI: Cả văn bản không toàn vẹn hoặc khóa bị thay đổi!", "#FF4D4D", "#1A0505", "#FF4D4D");
                    }
                    else if (msgChanged && !keyChanged)
                    {
                        SetResult("❌", "LỖI: Văn bản không toàn vẹn hoặc đã bị sửa đổi!", "#FF4D4D", "#1A0505", "#FF4D4D");
                    }
                    else if (!msgChanged && keyChanged)
                    {
                        SetResult("❌", "LỖI: Khóa bị sửa đổi hoặc sai!", "#FF4D4D", "#1A0505", "#FF4D4D");
                    }
                    else
                    {
                        SetResult("❌", "LỖI: Khóa bị sửa đổi hoặc sai!", "#FF4D4D", "#1A0505", "#FF4D4D");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError("Lỗi toán học", ex.Message);
                SetResult("❌", "❌ Lỗi trong quá trình xác thực\n\n" + ex.Message, "#FF4D4D", "#1A0505", "#FF4D4D");
            }
            finally
            {
                btnVerify.IsEnabled = true;
            }
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

        private void SetStatus(string msg) => txtStatusBar.Text = msg;

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
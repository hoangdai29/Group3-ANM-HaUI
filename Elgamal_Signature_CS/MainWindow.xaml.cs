using System;
using System.IO;
using System.Numerics;
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

        private BigInteger? _loadedQ;
        private BigInteger? _loadedA;
        private BigInteger? _loadedYA;
        private string _fileOriginalMessage = string.Empty;
        private BigInteger? _fileOriginalYA;

        public MainWindow()
        {
            InitializeComponent();
            btnSaveKeys.IsEnabled = false;
            btnSaveSignature.IsEnabled = false;
        }

        private async void BtnGenerateKeys_Click(object sender, RoutedEventArgs e)
        {
            btnGenerateKeys.IsEnabled = false; btnSaveKeys.IsEnabled = false;
            txtKeyStatus.Text = "⏳ Đang tạo khóa tự động tối ưu...";
            txtKeyStatus.Foreground = Brush("#78909C");

            int bits = cmbKeySize.SelectedIndex switch { 0 => 256, 1 => 512, 2 => 1024, _ => 512 };

            try
            {
                _key = await Task.Run(() => ElGamalCrypto.GenerateKeys(bits));
                txtQ.Text = _key.Q.ToString();
                txtA.Text = _key.A.ToString();
                txtYA.Text = _key.YA.ToString();
                txtXA.Text = _key.XA.ToString();

                txtKeyStatus.Text = $"✅ Đã sinh khóa tự động ({bits}-bit) thành công!";
                txtKeyStatus.Foreground = Brush("#2E7D32");
                btnSaveKeys.IsEnabled = true;
            }
            catch (Exception ex) { ShowError("Lỗi sinh khóa", ex.Message); }
            finally { btnGenerateKeys.IsEnabled = true; }
        }

        // TÍNH NĂNG MỚI: ÁP DỤNG THAM SỐ THỦ CÔNG TỪ ĐỀ BÀI
        private void BtnApplyManual_Click(object sender, RoutedEventArgs e)
        {
            string qStr = txtQ.Text.Trim();
            string aStr = txtA.Text.Trim();
            string xaStr = txtXA.Text.Trim();

            if (string.IsNullOrEmpty(qStr) || string.IsNullOrEmpty(aStr) || string.IsNullOrEmpty(xaStr))
            {
                ShowWarning("Vui lòng nhập đầy đủ các trường: q, a, và X_A để thiết lập thủ công!");
                return;
            }

            if (!BigInteger.TryParse(qStr, out BigInteger q) ||
                !BigInteger.TryParse(aStr, out BigInteger a) ||
                !BigInteger.TryParse(xaStr, out BigInteger xa))
            {
                ShowError("Lỗi định dạng", "Các tham số nhập vào phải là số nguyên hợp lệ.");
                return;
            }

            // Tính khóa công khai: Y_A = a^X_A mod q
            BigInteger ya = BigInteger.ModPow(a, xa, q);
            txtYA.Text = ya.ToString();

            _key = new ElGamalKeyPair { Q = q, A = a, XA = xa, YA = ya };

            btnSaveKeys.IsEnabled = true;
            txtKeyStatus.Text = "⚠️ Đang chạy ở chế độ cấu hình khóa Thủ Công!";
            txtKeyStatus.Foreground = Brush("#E65100");
            MessageBox.Show("Đã áp dụng cấu hình dữ liệu thủ công thành công!\nKhóa công khai Y_A đã tự động tính.", "Thành công");
        }

        private async void BtnSign_Click(object sender, RoutedEventArgs e)
        {
            if (_key == null) { ShowWarning("Vui lòng khởi tạo khóa (Tự động hoặc Thủ công) ở Tab 1!"); return; }
            string msg = txtMsgSign.Text.Trim();
            if (string.IsNullOrEmpty(msg)) { ShowWarning("Vui lòng nhập thông điệp M!"); return; }

            btnSign.IsEnabled = false;

            try
            {
                // HỖ TRỢ ĐỀ BÀI: Nếu M nhập vào là số nguyên thuần túy, lấy thẳng số nguyên, ngược lại băm SHA-256
                BigInteger hash;
                if (BigInteger.TryParse(msg, out BigInteger parsedMsgNum)) { hash = parsedMsgNum; }
                else { hash = ElGamalCrypto.HashToBigInteger(msg); }

                txtSignHash.Text = hash.ToString();

                string kStr = txtKInput.Text.Trim();
                // Nếu người dùng ép tham số k thủ công từ đề bài
                if (!string.IsNullOrEmpty(kStr) && BigInteger.TryParse(kStr, out BigInteger customK))
                {
                    BigInteger qMinus1 = _key.Q - 1;
                    if (ElGamalCrypto.GCD(customK, qMinus1) != 1)
                    {
                        ShowError("Lỗi toán học", "Tham số k nhập vào bắt buộc phải nguyên tố cùng nhau với (q-1)!");
                        return;
                    }

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
                    _currentSignature = await Task.Run(() => ElGamalCrypto.Sign(msg, _key!));
                }

                _currentSignedMessage = msg;
                txtSignR.Text = _currentSignature.R.ToString();
                txtSignS.Text = _currentSignature.S.ToString();
                txtSignatureBlock.Text = $"ELGAMAL-SIG[{_currentSignature.R.ToString("X").Substring(0, Math.Min(12, _currentSignature.R.ToString("X").Length))}...|{_currentSignature.S.ToString("X").Substring(0, Math.Min(12, _currentSignature.S.ToString("X").Length))}]";

                btnSaveSignature.IsEnabled = true;
                MessageBox.Show("Đã tạo chữ ký thành công!", "Thông báo");
            }
            catch (Exception ex) { ShowError("Lỗi ký", ex.Message); }
            finally { btnSign.IsEnabled = true; }
        }

        private async void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            string msgInput = txtMsgVerify.Text.Trim();
            string rStr = txtVerifyR.Text.Trim();
            string sStr = txtVerifyS.Text.Trim();

            if (string.IsNullOrEmpty(msgInput) || string.IsNullOrEmpty(rStr) || string.IsNullOrEmpty(sStr))
            { ShowWarning("Vui lòng điền đầy đủ thông tin!"); return; }

            if (_loadedQ == null || _loadedA == null || _loadedYA == null)
            { ShowWarning("Vui lòng nạp tệp khóa công khai (.pub) trước!"); return; }

            btnVerify.IsEnabled = false;
            borderResult.Background = Brush("#ECEFF1");
            txtResult.Text = "— Đang xác thực —";

            try
            {
                BigInteger r = BigInteger.Parse(rStr);
                BigInteger s = BigInteger.Parse(sStr);
                var sig = new ElGamalSig { R = r, S = s };
                var keyContext = new ElGamalKeyPair { Q = _loadedQ.Value, A = _loadedA.Value, YA = _loadedYA.Value };

                // HỖ TRỢ ĐỀ BÀI CHO XÁC THỰC SỐ THỦ CÔNG
                BigInteger hash;
                if (BigInteger.TryParse(msgInput, out BigInteger parsedMsgNum)) { hash = parsedMsgNum; }
                else { hash = ElGamalCrypto.HashToBigInteger(msgInput); }

                var result = await Task.Run(() => ElGamalCrypto.VerifyWithDetails(msgInput, sig, keyContext));

                // Ghi đè lại hash tính toán số học thực tế lên UI
                txtVHash.Text = "Giá trị thông điệp M dùng tính: " + hash.ToString();
                txtVLHS.Text = "Vế trái V1 = " + result.lhs.ToString();
                txtVRHS.Text = "Vế phải V2 = " + result.rhs.ToString();

                bool isMessageModified = (!string.IsNullOrEmpty(_fileOriginalMessage) && msgInput != _fileOriginalMessage);
                bool isKeyIncorrect = (_fileOriginalYA != null && _loadedYA.Value != _fileOriginalYA.Value);

                if (result.valid && !isMessageModified && !isKeyIncorrect)
                {
                    borderResult.Background = Brush("#C8E6C9");
                    txtResult.Text = "✅ CHỮ KÝ HỢP LỆ\nThông điệp toàn vẹn, nguồn gốc xác thực thành công!";
                    txtResult.Foreground = Brush("#1B5E20");
                }
                else
                {
                    borderResult.Background = Brush("#FFCDD2");
                    txtResult.Foreground = Brush("#B71C1C");

                    if (isMessageModified && isKeyIncorrect)
                        txtResult.Text = "❌ CHỮ KÝ SAI (CẢ HAI ĐỀU SAI)\n-> Sai nội dung thông điệp văn bản và sai cấu hình khóa.";
                    else if (isMessageModified)
                        txtResult.Text = "❌ CHỮ KÝ KHÔNG HỢP LỆ\n-> Lỗi: Nội dung thông điệp văn bản đã bị sửa đổi so với gốc!";
                    else if (isKeyIncorrect)
                        txtResult.Text = "❌ CHỮ KÝ KHÔNG HỢP LỆ\n-> Lỗi: Khóa công khai dùng xác thực không khớp với thực thể ký.";
                    else
                        txtResult.Text = "❌ CHỮ KÝ KHÔNG HỢP LỆ\nGiá trị số (r, s) không thỏa mãn đồng dư toán học.";
                }
            }
            catch (Exception ex) { ShowError("Lỗi", ex.Message); }
            finally { btnVerify.IsEnabled = true; }
        }

        private void BtnSaveKeys_Click(object sender, RoutedEventArgs e)
        {
            if (_key == null) return;
            SaveFileDialog saveKeyDialog = new SaveFileDialog { Filter = "Khóa công khai (*.pub)|*.pub" };
            if (saveKeyDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveKeyDialog.FileName, $"{_key.Q}\n{_key.A}\n{_key.YA}");
                    File.WriteAllText(Path.ChangeExtension(saveKeyDialog.FileName, ".pri"), _key.XA.ToString());
                    MessageBox.Show("Đã lưu khóa thành công!", "Thành công");
                }
                catch (Exception ex) { ShowError("Lỗi", ex.Message); }
            }
        }

        private void BtnSaveSignature_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSignature == null || _key == null) return;
            SaveFileDialog saveSigDialog = new SaveFileDialog { Filter = "File chữ ký (*.sig)|*.sig" };
            if (saveSigDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveSigDialog.FileName, $"{_currentSignature.R}\n{_currentSignature.S}\n{_key.YA}\n{_currentSignedMessage}");
                    MessageBox.Show("Đã lưu chữ ký thành công!", "Thành công");
                }
                catch (Exception ex) { ShowError("Lỗi", ex.Message); }
            }
        }

        private void BtnLoadPublicKey_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog { Filter = "Khóa công khai (*.pub)|*.pub" };
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(openDialog.FileName);
                    if (lines.Length >= 3 && BigInteger.TryParse(lines[0], out BigInteger q) && BigInteger.TryParse(lines[1], out BigInteger a) && BigInteger.TryParse(lines[2], out BigInteger ya))
                    {
                        _loadedQ = q; _loadedA = a; _loadedYA = ya;
                        MessageBox.Show("Nạp khóa công khai thành công!", "Thành công");
                    }
                }
                catch (Exception ex) { ShowError("Lỗi", ex.Message); }
            }
        }

        private void BtnLoadSignatureFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog { Filter = "File chữ ký (*.sig)|*.sig" };
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(openDialog.FileName);
                    if (lines.Length >= 4 && BigInteger.TryParse(lines[0], out BigInteger r) && BigInteger.TryParse(lines[1], out BigInteger s) && BigInteger.TryParse(lines[2], out BigInteger origYA))
                    {
                        txtVerifyR.Text = r.ToString(); txtVerifyS.Text = s.ToString(); _fileOriginalYA = origYA;
                        _fileOriginalMessage = string.Join(Environment.NewLine, lines, 3, lines.Length - 3);
                        txtMsgVerify.Text = _fileOriginalMessage;
                        MessageBox.Show("Nạp file chữ ký thành công!", "Thành công");
                    }
                }
                catch (Exception ex) { ShowError("Lỗi", ex.Message); }
            }
        }

        private static SolidColorBrush Brush(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        private static void ShowWarning(string msg) => MessageBox.Show(msg, "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
        private static void ShowError(string title, string msg) => MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
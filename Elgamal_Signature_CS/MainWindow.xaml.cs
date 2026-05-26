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

        // Bộ lưu trữ dữ liệu nạp từ tệp tin (.pub) phục vụ độc lập cho Tab Xác thực
        private BigInteger? _loadedQ;
        private BigInteger? _loadedA;
        private BigInteger? _loadedYA;

        public MainWindow()
        {
            InitializeComponent();
            btnSaveKeys.IsEnabled = false;
            btnSaveSignature.IsEnabled = false;
        }

        // ==========================================
        // TAB 1 — SINH VÀ LƯU KHÓA HỆ THỐNG
        // ==========================================
        private async void BtnGenerateKeys_Click(object sender, RoutedEventArgs e)
        {
            btnGenerateKeys.IsEnabled = false;
            btnSaveKeys.IsEnabled = false;
            txtKeyStatus.Text = "⏳ Đang tạo khóa...";
            txtKeyStatus.Foreground = Brush("#78909C");

            txtQ.Text = txtA.Text = txtXA.Text = txtYA.Text = "";

            int bits = cmbKeySize.SelectedIndex switch
            {
                0 => 256,
                1 => 512,
                2 => 1024,
                _ => 512
            };

            try
            {
                _key = await Task.Run(() => ElGamalCrypto.GenerateKeys(bits));

                txtQ.Text = _key.Q.ToString();
                txtA.Text = _key.A.ToString();
                txtYA.Text = _key.YA.ToString();
                txtXA.Text = _key.XA.ToString();

                txtKeyStatus.Text = $"✅ Đã tạo xong ({bits}-bit)";
                txtKeyStatus.Foreground = Brush("#2E7D32");

                btnSaveKeys.IsEnabled = true;
                MessageBox.Show($"Cặp khóa {bits}-bit đã được khởi tạo thành công!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Lỗi tạo khóa", ex.Message);
                txtKeyStatus.Text = "❌ Lỗi tạo khóa";
                txtKeyStatus.Foreground = Brush("#C62828");
            }
            finally
            {
                btnGenerateKeys.IsEnabled = true;
            }
        }

        private void BtnSaveKeys_Click(object sender, RoutedEventArgs e)
        {
            if (_key == null) return;

            SaveFileDialog saveKeyDialog = new SaveFileDialog
            {
                Filter = "Khóa công khai (*.pub)|*.pub",
                Title = "Lưu thông tin khóa công khai cấu hình hệ thống"
            };

            if (saveKeyDialog.ShowDialog() == true)
            {
                try
                {
                    // Xuất tệp công khai chứa bộ thông số (q, a, Y_A)
                    string pubKeyData = $"{_key.Q}\n{_key.A}\n{_key.YA}";
                    File.WriteAllText(saveKeyDialog.FileName, pubKeyData);

                    // Tự động sinh đường dẫn lưu file khóa bí mật (.pri) kế bên tệp tin công khai
                    string privateKeyPath = Path.ChangeExtension(saveKeyDialog.FileName, ".pri");
                    File.WriteAllText(privateKeyPath, _key.XA.ToString());

                    MessageBox.Show($"Đã xuất lưu tệp tin khóa thành công!\n\n1. File khóa công khai: {Path.GetFileName(saveKeyDialog.FileName)}\n2. File khóa bí mật: {Path.GetFileName(privateKeyPath)}", "Lưu file thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    ShowError("Lỗi ghi lưu tệp", ex.Message);
                }
            }
        }

        // ==========================================
        // TAB 2 — KÝ SỐ VÀ LƯU FILE CHỮ KÝ (.sig)
        // ==========================================
        private async void BtnSign_Click(object sender, RoutedEventArgs e)
        {
            if (_key == null)
            {
                ShowWarning("Không tìm thấy dữ liệu khóa riêng tư phù hợp trên RAM để thực hiện ký số.\nVui lòng khởi tạo khóa tại Tab 1.");
                return;
            }

            string msg = txtMsgSign.Text.Trim();
            if (string.IsNullOrEmpty(msg))
            {
                ShowWarning("Vui lòng nhập nội dung thông điệp cần thực hiện ký!");
                return;
            }

            btnSign.IsEnabled = false;
            btnSaveSignature.IsEnabled = false;
            txtSignHash.Text = txtSignR.Text = txtSignS.Text = "";

            try
            {
                BigInteger hash = ElGamalCrypto.HashToBigInteger(msg);
                txtSignHash.Text = hash.ToString();

                _currentSignature = await Task.Run(() => ElGamalCrypto.Sign(msg, _key!));
                _currentSignedMessage = msg;

                txtSignR.Text = _currentSignature.R.ToString();
                txtSignS.Text = _currentSignature.S.ToString();

                btnSaveSignature.IsEnabled = true;
                MessageBox.Show("Đã ký số thông điệp thành công.\nHãy bấm 'Lưu Chữ Ký Số' để xuất tệp lưu trữ.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError("Lỗi hệ thống ký số", ex.Message);
            }
            finally
            {
                btnSign.IsEnabled = true;
            }
        }

        private void BtnSaveSignature_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSignature == null || string.IsNullOrEmpty(_currentSignedMessage)) return;

            SaveFileDialog saveSigDialog = new SaveFileDialog
            {
                Filter = "File chữ ký số ElGamal (*.sig)|*.sig",
                Title = "Lưu tệp tin thông điệp kèm cặp chữ ký số"
            };

            if (saveSigDialog.ShowDialog() == true)
            {
                try
                {
                    // Cấu trúc file .sig chuẩn: Dòng 1 chứa r | Dòng 2 chứa s | Dòng 3 trở đi chứa thông điệp gốc M
                    string fileContent = $"{_currentSignature.R}\n{_currentSignature.S}\n{_currentSignedMessage}";
                    File.WriteAllText(saveSigDialog.FileName, fileContent);

                    MessageBox.Show("Thông điệp kèm tệp tin chữ ký số đã được xuất lưu thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    ShowError("Lỗi xuất file chữ ký", ex.Message);
                }
            }
        }

        // ==========================================
        // TAB 3 — XÁC THỰC DỰA TRÊN FILE ĐỘC LẬP
        // ==========================================
        private void BtnLoadPublicKey_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "File khóa công khai (*.pub)|*.pub",
                Title = "Nạp thông số khóa công khai cấu hình hệ thống"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(openDialog.FileName);
                    if (lines.Length >= 3 &&
                        BigInteger.TryParse(lines[0], out BigInteger q) &&
                        BigInteger.TryParse(lines[1], out BigInteger a) &&
                        BigInteger.TryParse(lines[2], out BigInteger ya))
                    {
                        _loadedQ = q;
                        _loadedA = a;
                        _loadedYA = ya;

                        MessageBox.Show("Nạp dữ liệu khóa công khai (q, a, Y_A) từ tệp tin thành công!", "Nạp thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ShowError("Lỗi cấu trúc tệp tin", "File khóa công khai không đúng cấu trúc dòng quy định.");
                    }
                }
                catch (Exception ex)
                {
                    ShowError("Lỗi đọc file", ex.Message);
                }
            }
        }

        private void BtnLoadSignatureFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog
            {
                Filter = "File chữ ký số ElGamal (*.sig)|*.sig",
                Title = "Nạp tệp tin chữ ký và thông điệp xác thực"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(openDialog.FileName);
                    if (lines.Length >= 3 &&
                        BigInteger.TryParse(lines[0], out BigInteger r) &&
                        BigInteger.TryParse(lines[1], out BigInteger s))
                    {
                        txtVerifyR.Text = r.ToString();
                        txtVerifyS.Text = s.ToString();

                        // Trích xuất chuỗi nội dung thông điệp gốc từ dòng số 3 trở đi
                        string msgContent = string.Join(Environment.NewLine, lines, 2, lines.Length - 2);
                        txtMsgVerify.Text = msgContent;

                        MessageBox.Show("Nạp thông điệp và cặp giá trị chữ ký (r, s) từ file thành công!", "Nạp thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ShowError("Lỗi cấu trúc chữ ký", "Định dạng tệp tin .sig lỗi cấu trúc dữ liệu.");
                    }
                }
                catch (Exception ex)
                {
                    ShowError("Lỗi đọc file chữ ký", ex.Message);
                }
            }
        }

        private async void BtnVerify_Click(object sender, RoutedEventArgs e)
        {
            string msg = txtMsgVerify.Text.Trim();
            string rStr = txtVerifyR.Text.Trim();
            string sStr = txtVerifyS.Text.Trim();

            if (string.IsNullOrEmpty(msg) || string.IsNullOrEmpty(rStr) || string.IsNullOrEmpty(sStr))
            {
                ShowWarning("Vui lòng điền nội dung text hoặc thực hiện bấm nạp tệp tin chữ ký trước!");
                return;
            }

            if (_loadedQ == null || _loadedA == null || _loadedYA == null)
            {
                ShowWarning("Chưa tìm thấy dữ liệu khóa công khai xác thực.\nHãy nhấn 'Nạp Khóa Công Khai (.pub)' để chọn tệp lưu trữ.");
                return;
            }

            if (!BigInteger.TryParse(rStr, out BigInteger r) || !BigInteger.TryParse(sStr, out BigInteger s))
            {
                ShowError("Sai định dạng số", "Giá trị r hoặc s hiển thị trên khung nhập không phải là số nguyên hợp lệ.");
                return;
            }

            btnVerify.IsEnabled = false;
            txtVHash.Text = txtVLHS.Text = txtVRHS.Text = "";
            SetVerifyResult(null, null);

            try
            {
                var sig = new ElGamalSig { R = r, S = s };
                var keyContext = new ElGamalKeyPair { Q = _loadedQ.Value, A = _loadedA.Value, YA = _loadedYA.Value };

                var result = await Task.Run(() => ElGamalCrypto.VerifyWithDetails(msg, sig, keyContext));

                txtVHash.Text = result.hash.ToString();
                txtVLHS.Text = result.lhs.ToString();
                txtVRHS.Text = result.rhs.ToString();

                SetVerifyResult(result.valid, result.lhs == result.rhs);
            }
            catch (Exception ex)
            {
                ShowError("Lỗi quy trình xác thực", ex.Message);
            }
            finally
            {
                btnVerify.IsEnabled = true;
            }
        }

        // ==========================================
        // CÁC HÀM PHỤ TRỢ (Copy, UI Brush)
        // ==========================================
        private void CopyQ_Click(object sender, RoutedEventArgs e) => SafeCopy(txtQ.Text);
        private void CopyA_Click(object sender, RoutedEventArgs e) => SafeCopy(txtA.Text);
        private void CopyXA_Click(object sender, RoutedEventArgs e) => SafeCopy(txtXA.Text);
        private void CopyYA_Click(object sender, RoutedEventArgs e) => SafeCopy(txtYA.Text);
        private void CopyR_Click(object sender, RoutedEventArgs e) => SafeCopy(txtSignR.Text);
        private void CopyS_Click(object sender, RoutedEventArgs e) => SafeCopy(txtSignS.Text);

        private static void SafeCopy(string text)
        {
            if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text);
        }

        private void SetVerifyResult(bool? valid, bool? sidesEqual)
        {
            if (valid == null)
            {
                borderResult.Background = Brush("#ECEFF1");
                txtResult.Text = "— Chưa xác thực —";
                txtResult.Foreground = Brush("#78909C");
                return;
            }

            if (valid == true)
            {
                borderResult.Background = Brush("#C8E6C9");
                txtResult.Text = "✅ CHỮ KÝ HỢP LỆ\n" +
                                 "Thông điệp toàn vẹn và xác thực thành công!\n" +
                                 "V1 ≡ V2 (a^M ≡ Y_A^r · r^s mod q) ✔";
                txtResult.Foreground = Brush("#1B5E20");
            }
            else
            {
                borderResult.Background = Brush("#FFCDD2");
                txtResult.Text = "❌ CHỮ KÝ KHÔNG HỢP LỆ\n" +
                                 "Thông điệp đã bị thay đổi hoặc cặp chữ ký sai!\n" +
                                 "V1 ≢ V2 (a^M ≢ Y_A^r · r^s mod q) ✘";
                txtResult.Foreground = Brush("#B71C1C");
            }
        }

        private void SetSignVerifyEnabled(bool enabled)
        {
            btnSign.IsEnabled = enabled;
        }

        private static SolidColorBrush Brush(string hex)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new SolidColorBrush(c);
        }

        private static void ShowWarning(string msg) =>
            MessageBox.Show(msg, "Thông báo hệ thống", MessageBoxButton.OK, MessageBoxImage.Warning);

        private static void ShowError(string title, string msg) =>
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
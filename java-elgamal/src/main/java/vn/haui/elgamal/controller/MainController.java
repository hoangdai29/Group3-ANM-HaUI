package vn.haui.elgamal.controller;

import javafx.concurrent.Task;
import javafx.fxml.FXML;
import javafx.scene.control.*;
import javafx.scene.layout.VBox;
import javafx.stage.FileChooser;
import vn.haui.elgamal.model.ElGamalModel;
import vn.haui.elgamal.model.KeyPairData;
import vn.haui.elgamal.model.SignatureData;
import vn.haui.elgamal.service.FileService;
import vn.haui.elgamal.service.ValidationService;

import java.io.File;
import java.math.BigInteger;
import java.nio.charset.StandardCharsets;
import java.time.LocalTime;
import java.time.format.DateTimeFormatter;
import java.util.Base64;
import java.util.Optional;

/**
 * Lớp điều khiển chính (Controller) duy nhất của ứng dụng Chữ Ký Số ElGamal.
 * Liên kết FXML, bắt các sự kiện UI, gọi xử lý toán học từ Model và tương tác tệp tin từ Service.
 */
public class MainController {

    // --- FXML BƯỚC 1: TẠO KHÓA ---
    @FXML private ComboBox<Integer> cbBitLength;
    @FXML private Button btnAutoKey;
    @FXML private Button btnManualMode;
    @FXML private ProgressIndicator progressKeyGen;
    
    @FXML private VBox vboxManualInput;
    @FXML private TextField tfManualP;
    @FXML private Label lblErrorP;
    @FXML private TextField tfManualAlpha;
    @FXML private Label lblErrorAlpha;
    @FXML private TextField tfManualX;
    @FXML private Label lblErrorX;
    @FXML private Button btnApplyManualKey;
    
    @FXML private TextArea taKeyP;
    @FXML private TextArea taKeyAlpha;
    @FXML private TextArea taKeyX;
    @FXML private TextArea taKeyY;
    @FXML private Button btnSaveKeys;

    // --- FXML BƯỚC 2: KÝ TÀI LIỆU ---
    @FXML private ComboBox<String> cbSignSourceType;
    @FXML private VBox vboxSignText;
    @FXML private TextArea taSignInput;
    @FXML private VBox vboxSignFile;
    @FXML private Button btnSelectSignFile;
    @FXML private Label lblSignFilePath;
    @FXML private Button btnSign;
    @FXML private ProgressIndicator progressSign;
    @FXML private TextArea taSignOutput;
    @FXML private Button btnSaveSignature;

    // --- FXML BƯỚC 3: XÁC MINH CHỮ KÝ ---
    @FXML private ComboBox<String> cbVerifySourceType;
    @FXML private VBox vboxVerifyText;
    @FXML private TextArea taVerifyInput;
    @FXML private VBox vboxVerifyFile;
    @FXML private Button btnSelectVerifyFile;
    @FXML private Label lblVerifyFilePath;
    @FXML private TextArea taVerifySignature;
    @FXML private Button btnSelectSigFile;
    @FXML private TextArea taVerifyPubY;
    @FXML private Button btnVerify;
    @FXML private ProgressIndicator progressVerify;
    @FXML private VBox vboxResultCard;
    @FXML private Label lblVerifyResult;

    // --- FXML DƯỚI CÙNG: NHẬT KÝ HÀ HÀNH ĐỘNG ---
    @FXML private Button btnClearLog;
    @FXML private Button btnResetAll;
    @FXML private ListView<String> lvLogs;

    // --- Trạng thái nghiệp vụ (State) ---
    private ElGamalModel model;
    private KeyPairData currentKeyPair;
    private File selectedSignFile;
    private File selectedVerifyFile;
    private File selectedSigFile;

    private static final String SOURCE_TEXT = "Văn bản trực tiếp";
    private static final String SOURCE_FILE = "Chọn file tài liệu";
    private final DateTimeFormatter timeFormatter = DateTimeFormatter.ofPattern("HH:mm:ss");

    /**
     * Phương thức khởi tạo mặc định của JavaFX Controller.
     * Liên kết các sự kiện thay đổi dữ liệu, cấu hình mặc định cho các ComboBox.
     */
    @FXML
    public void initialize() {
        model = new ElGamalModel();

        // 1. Cấu hình Cột 1: Tạo khóa
        cbBitLength.getItems().addAll(512, 1024);
        cbBitLength.setValue(512); // Bit length mặc định là 512-bit để demo nhanh

        // Đăng ký sự kiện lắng nghe thay đổi nội dung nhập tay để kiểm định inline lập tức
        tfManualP.textProperty().addListener((observable, oldValue, newValue) -> validateManualP());
        tfManualAlpha.textProperty().addListener((observable, oldValue, newValue) -> validateManualAlpha());
        tfManualX.textProperty().addListener((observable, oldValue, newValue) -> validateManualX());

        // 2. Cấu hình Cột 2: Ký tài liệu
        cbSignSourceType.getItems().addAll(SOURCE_TEXT, SOURCE_FILE);
        cbSignSourceType.setValue(SOURCE_TEXT);

        // 3. Cấu hình Cột 3: Xác minh
        cbVerifySourceType.getItems().addAll(SOURCE_TEXT, SOURCE_FILE);
        cbVerifySourceType.setValue(SOURCE_TEXT);

        // 4. Liên kết tự động điền khóa công khai y từ Cột 1 sang Cột 3 để nâng cao UX
        taKeyY.textProperty().addListener((observable, oldValue, newValue) -> {
            if (newValue != null && !newValue.trim().isEmpty()) {
                taVerifyPubY.setText(newValue.trim());
            }
        });

        // Nhật ký khởi động
        addLog("HỆ THỐNG", "Chương trình khởi chạy thành công. Bit-length mặc định: 512-bit.");
    }

    // ==========================================
    // PHẦN XỬ LÝ: BƯỚC 1 - SINH/NHẬP KHÓA
    // ==========================================

    /**
     * Sự kiện click nút "Sinh tự động" của Cột 1.
     * Chạy phép toán sinh số nguyên tố lớn và căn nguyên thủy trên một Task luồng phụ để giữ UI mượt mà.
     */
    @FXML
    private void handleAutoKeyGen() {
        int bitLength = cbBitLength.getValue();

        // Vô hiệu hóa tạm thời các nút để tránh click dồn dập, bật Spinner
        btnAutoKey.setDisable(true);
        btnManualMode.setDisable(true);
        progressKeyGen.setVisible(true);

        addLog("SINH KHÓA", "Bắt đầu sinh khóa ElGamal " + bitLength + "-bit (Chạy trên luồng phụ)...");

        // Tạo Task chạy ngầm
        Task<KeyPairData> task = new Task<>() {
            @Override
            protected KeyPairData call() {
                return model.sinhKhoaTuDong(bitLength);
            }
        };

        // Khi tiến trình hoàn thành thành công
        task.setOnSucceeded(e -> {
            currentKeyPair = task.getValue();

            // Hiển thị khóa lên UI
            taKeyP.setText(currentKeyPair.getP().toString());
            taKeyAlpha.setText(currentKeyPair.getAlpha().toString());
            taKeyX.setText(currentKeyPair.getX().toString());
            taKeyY.setText(currentKeyPair.getY().toString());

            // Mở lại UI tương tác
            btnAutoKey.setDisable(false);
            btnManualMode.setDisable(false);
            progressKeyGen.setVisible(false);

            addLog("SINH KHÓA", "Sinh thành công khóa ElGamal " + bitLength + "-bit (Safe Prime).");
        });

        // Khi tiến trình thất bại
        task.setOnFailed(e -> {
            btnAutoKey.setDisable(false);
            btnManualMode.setDisable(false);
            progressKeyGen.setVisible(false);

            Throwable ex = task.getException();
            addLog("LỖI SINH KHÓA", "Gặp lỗi khi sinh khóa: " + ex.getMessage());
        });

        new Thread(task).start();
    }

    /**
     * Bật/Tắt khung nhập liệu thủ công
     */
    @FXML
    private void toggleManualMode() {
        boolean isVisible = vboxManualInput.isVisible();
        vboxManualInput.setVisible(!isVisible);
        vboxManualInput.setManaged(!isVisible);

        if (!isVisible) {
            btnManualMode.setText("Ẩn nhập thủ công");
            addLog("CHẾ ĐỘ KHÓA", "Mở bảng cấu hình tham số khóa thủ công.");
        } else {
            btnManualMode.setText("Nhập thủ công");
            addLog("CHẾ ĐỘ KHÓA", "Đóng bảng cấu hình tham số khóa thủ công.");
        }
    }

    /**
     * Sự kiện nút "Áp dụng khóa" tự chọn.
     * Validate dữ liệu đầu vào. Nếu hợp lệ, tính y = alpha^x mod p và thiết lập cặp khóa.
     */
    @FXML
    private void handleApplyManualKey() {
        // Thực hiện kiểm định đồng bộ toàn bộ 3 trường
        boolean isPValid = validateManualP();
        boolean isAlphaValid = validateManualAlpha();
        boolean isXValid = validateManualX();

        if (isPValid && isAlphaValid && isXValid) {
            try {
                BigInteger p = new BigInteger(tfManualP.getText().trim());
                BigInteger alpha = new BigInteger(tfManualAlpha.getText().trim());
                BigInteger x = new BigInteger(tfManualX.getText().trim());

                // Tính y và đóng gói khóa
                currentKeyPair = model.sinhKhoaTuChon(p, alpha, x);

                taKeyP.setText(currentKeyPair.getP().toString());
                taKeyAlpha.setText(currentKeyPair.getAlpha().toString());
                taKeyX.setText(currentKeyPair.getX().toString());
                taKeyY.setText(currentKeyPair.getY().toString());

                addLog("NHẬP KHÓA TAY", "Áp dụng thành công khóa nhập tay. p = " + p.bitLength() + "-bit.");
            } catch (Exception e) {
                addLog("LỖI NHẬP KHÓA", "Lỗi bất ngờ khi áp dụng khóa: " + e.getMessage());
            }
        } else {
            addLog("LỖI NHẬP KHÓA", "Không thể áp dụng khóa: Các tham số chưa vượt qua kiểm định.");
        }
    }

    // --- Các hàm kiểm định inline kết nối ValidationService ---

    private boolean validateManualP() {
        String text = tfManualP.getText().trim();
        if (text.isEmpty()) {
            showErrorLabel(lblErrorP, "❌ Số p không được để trống.");
            return false;
        }
        try {
            BigInteger p = new BigInteger(text);
            if (!ValidationService.isPrime(p)) {
                showErrorLabel(lblErrorP, "❌ p phải là số nguyên tố.");
                return false;
            }
            hideErrorLabel(lblErrorP);
            return true;
        } catch (NumberFormatException e) {
            showErrorLabel(lblErrorP, "❌ p phải là một số nguyên hợp lệ.");
            return false;
        }
    }

    private boolean validateManualAlpha() {
        String textP = tfManualP.getText().trim();
        String textAlpha = tfManualAlpha.getText().trim();

        if (textAlpha.isEmpty()) {
            showErrorLabel(lblErrorAlpha, "❌ alpha không được để trống.");
            return false;
        }

        // Bắt buộc phải có số p hợp lệ trước để kiểm tra alpha modulo p
        if (textP.isEmpty() || !validateManualP()) {
            showErrorLabel(lblErrorAlpha, "❌ Vui lòng nhập số p nguyên tố hợp lệ trước.");
            return false;
        }

        try {
            BigInteger p = new BigInteger(textP);
            BigInteger alpha = new BigInteger(textAlpha);

            if (!ValidationService.isPrimitiveRoot(alpha, p)) {
                showErrorLabel(lblErrorAlpha, "❌ alpha không phải căn nguyên thủy modulo p.");
                return false;
            }

            // Nếu alpha là căn nguyên thủy modulo p nhưng đang sử dụng Heuristic Check (đối với prime thường)
            if (ValidationService.isHeuristicPrimitiveRootCheck(p)) {
                showWarningLabel(lblErrorAlpha, "⚠️ Khóa thường: Không thể xác minh đầy đủ alpha do p-1 quá lớn (đã kiểm tra heuristic).");
            } else {
                hideErrorLabel(lblErrorAlpha);
            }
            return true;
        } catch (NumberFormatException e) {
            showErrorLabel(lblErrorAlpha, "❌ alpha phải là số nguyên hợp lệ.");
            return false;
        }
    }

    private boolean validateManualX() {
        String textP = tfManualP.getText().trim();
        String textX = tfManualX.getText().trim();

        if (textX.isEmpty()) {
            showErrorLabel(lblErrorX, "❌ Khóa bí mật x không được để trống.");
            return false;
        }

        if (textP.isEmpty() || !validateManualP()) {
            showErrorLabel(lblErrorX, "❌ Vui lòng nhập số p hợp lệ trước.");
            return false;
        }

        try {
            BigInteger p = new BigInteger(textP);
            BigInteger x = new BigInteger(textX);

            if (!ValidationService.isValidPrivateKey(x, p)) {
                showErrorLabel(lblErrorX, "❌ Ràng buộc bắt buộc: 1 < x < p-1.");
                return false;
            }
            hideErrorLabel(lblErrorX);
            return true;
        } catch (NumberFormatException e) {
            showErrorLabel(lblErrorX, "❌ x phải là số nguyên hợp lệ.");
            return false;
        }
    }

    private void showErrorLabel(Label label, String message) {
        label.setText(message);
        label.setStyle("-fx-text-fill: #dc2626;"); // Đỏ đậm báo lỗi
        label.setManaged(true);
        label.setVisible(true);
    }

    private void showWarningLabel(Label label, String message) {
        label.setText(message);
        label.setStyle("-fx-text-fill: #d97706;"); // Cam đất cảnh báo nhẹ
        label.setManaged(true);
        label.setVisible(true);
    }

    private void hideErrorLabel(Label label) {
        label.setManaged(false);
        label.setVisible(false);
    }

    // --- Các hàm Copy dữ liệu nhanh ra Clipboard ---

    @FXML private void copyP() { copyToClipboard(taKeyP.getText(), "p"); }
    @FXML private void copyAlpha() { copyToClipboard(taKeyAlpha.getText(), "alpha"); }
    @FXML private void copyX() { copyToClipboard(taKeyX.getText(), "x"); }
    @FXML private void copyY() { copyToClipboard(taKeyY.getText(), "y"); }

    private void copyToClipboard(String content, String name) {
        if (content == null || content.trim().isEmpty()) {
            addLog("COPY LỖI", "Không có dữ liệu " + name + " để copy.");
            return;
        }
        javafx.scene.input.Clipboard clipboard = javafx.scene.input.Clipboard.getSystemClipboard();
        javafx.scene.input.ClipboardContent cbContent = new javafx.scene.input.ClipboardContent();
        cbContent.putString(content.trim());
        clipboard.setContent(cbContent);
        addLog("COPY", "Đã copy giá trị " + name + " vào Clipboard.");
    }

    /**
     * Sự kiện nút "Lưu khóa ra file"
     */
    @FXML
    private void handleSaveKeys() {
        if (currentKeyPair == null) {
            addLog("LƯU KHÓA THẤT BẠI", "Không tồn tại thông tin khóa nào để lưu.");
            return;
        }

        FileChooser fileChooser = new FileChooser();
        fileChooser.setTitle("Lưu tệp tin khóa ElGamal");
        fileChooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("Tệp văn bản (*.txt)", "*.txt"));
        File file = fileChooser.showSaveDialog(taKeyP.getScene().getWindow());

        if (file != null) {
            try {
                FileService.writeKeyFile(file, currentKeyPair);
                addLog("LƯU KHÓA", "Đã lưu thông tin khóa dạng tệp cấu hình tại: " + file.getName());
            } catch (Exception e) {
                addLog("LỖI LƯU KHÓA", "Lỗi ghi tệp khóa: " + e.getMessage());
            }
        }
    }

    // ==========================================
    // PHẦN XỬ LÝ: BƯỚC 2 - KÝ TÀI LIỆU
    // ==========================================

    @FXML
    private void handleSignSourceTypeChange() {
        String type = cbSignSourceType.getValue();
        if (type.equals(SOURCE_FILE)) {
            vboxSignText.setVisible(false);
            vboxSignText.setManaged(false);
            vboxSignFile.setVisible(true);
            vboxSignFile.setManaged(true);
            addLog("NGUỒN KÝ", "Đã chuyển sang chế độ Ký Tệp Tin nhị phân.");
        } else {
            vboxSignText.setVisible(true);
            vboxSignText.setManaged(true);
            vboxSignFile.setVisible(false);
            vboxSignFile.setManaged(false);
            addLog("NGUỒN KÝ", "Đã chuyển sang chế độ Ký Văn Bản trực tiếp.");
        }
    }

    @FXML
    private void handleSelectSignFile() {
        FileChooser fileChooser = new FileChooser();
        fileChooser.setTitle("Chọn tệp tin tài liệu để ký số");
        File file = fileChooser.showOpenDialog(taSignInput.getScene().getWindow());

        if (file != null) {
            selectedSignFile = file;
            lblSignFilePath.setText(file.getName() + " (" + file.length() + " bytes)");
            addLog("CHỌN FILE KÝ", "Đã chọn file: " + file.getAbsolutePath());
        }
    }

    /**
     * Sự kiện nút "Ký tài liệu".
     * Quy đổi toàn bộ tài liệu (dù là text hay file nhị phân) ra mảng byte và thực hiện ký trên Task luồng phụ.
     */
    @FXML
    private void handleSign() {
        if (currentKeyPair == null || !currentKeyPair.hasPrivateKey()) {
            addLog("LỖI KÝ SỐ", "Thiếu khóa bí mật x. Vui lòng tạo khóa ở Bước 1 trước.");
            return;
        }

        final byte[] dataToSign;
        final String sourceDescription;

        if (cbSignSourceType.getValue().equals(SOURCE_FILE)) {
            if (selectedSignFile == null) {
                addLog("LỖI KÝ SỐ", "Chưa chọn tệp tin nhị phân nào để ký.");
                return;
            }
            try {
                dataToSign = FileService.readBytesFromFile(selectedSignFile);
                sourceDescription = "tệp tin nhị phân [" + selectedSignFile.getName() + "]";
            } catch (Exception e) {
                addLog("LỖI ĐỌC FILE", "Không thể đọc tệp để ký: " + e.getMessage());
                return;
            }
        } else {
            String text = taSignInput.getText();
            if (text.isEmpty()) {
                addLog("LỖI KÝ SỐ", "Nội dung văn bản trực tiếp để ký đang trống.");
                return;
            }
            dataToSign = text.getBytes(StandardCharsets.UTF_8);
            sourceDescription = "văn bản trực tiếp UTF-8";
        }

        // Tắt tương tác UI ở Cột 2, bật Spinner
        btnSign.setDisable(true);
        progressSign.setVisible(true);
        addLog("KÝ SỐ", "Đang tính toán băm SHA-256 và sinh chữ ký số ElGamal cho " + sourceDescription + "...");

        Task<SignatureData> task = new Task<>() {
            @Override
            protected SignatureData call() {
                // Thuật toán ký toán học thuần túy
                return model.kyVanBan(dataToSign, currentKeyPair);
            }
        };

        task.setOnSucceeded(e -> {
            SignatureData sig = task.getValue();

            // Mã hóa r, s sang Base64 và phân tách bằng '|' để xuất ra màn hình
            String rBase64 = Base64.getEncoder().encodeToString(sig.getR().toByteArray());
            String sBase64 = Base64.getEncoder().encodeToString(sig.getS().toByteArray());
            String outputStr = rBase64 + "|" + sBase64;

            taSignOutput.setText(outputStr);

            btnSign.setDisable(false);
            progressSign.setVisible(false);

            addLog("KÝ SỐ", "Ký số THÀNH CÔNG cho " + sourceDescription + ".");
        });

        task.setOnFailed(e -> {
            btnSign.setDisable(false);
            progressSign.setVisible(false);

            Throwable ex = task.getException();
            addLog("LỖI KÝ SỐ", "Thất bại trong quá trình ký: " + ex.getMessage());
        });

        new Thread(task).start();
    }

    @FXML
    private void handleSaveSignature() {
        String sigStr = taSignOutput.getText().trim();
        if (sigStr.isEmpty()) {
            addLog("LƯU CHỮ KÝ LỖI", "Không có chữ ký số nào được sinh để lưu.");
            return;
        }

        FileChooser fileChooser = new FileChooser();
        fileChooser.setTitle("Lưu tệp tin chữ ký (.sig)");
        fileChooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("Tệp chữ ký (*.sig)", "*.sig"));
        File file = fileChooser.showSaveDialog(taSignOutput.getScene().getWindow());

        if (file != null) {
            try {
                FileService.writeSignatureFile(file, sigStr);
                addLog("LƯU CHỮ KÝ", "Lưu thành công chữ ký Base64 ra file: " + file.getName());
            } catch (Exception e) {
                addLog("LỖI LƯU CHỮ KÝ", "Gặp lỗi khi lưu file chữ ký: " + e.getMessage());
            }
        }
    }

    // ==========================================
    // PHẦN XỬ LÝ: BƯỚC 3 - XÁC MINH CHỮ KÝ
    // ==========================================

    @FXML
    private void handleVerifySourceTypeChange() {
        String type = cbVerifySourceType.getValue();
        if (type.equals(SOURCE_FILE)) {
            vboxVerifyText.setVisible(false);
            vboxVerifyText.setManaged(false);
            vboxVerifyFile.setVisible(true);
            vboxVerifyFile.setManaged(true);
            addLog("NGUỒN XÁC MINH", "Đã chuyển sang chế độ Xác Minh Tệp Tin nhị phân.");
        } else {
            vboxVerifyText.setVisible(true);
            vboxVerifyText.setManaged(true);
            vboxVerifyFile.setVisible(false);
            vboxVerifyFile.setManaged(false);
            addLog("NGUỒN XÁC MINH", "Đã chuyển sang chế độ Xác Minh Văn Bản trực tiếp.");
        }
    }

    @FXML
    private void handleSelectVerifyFile() {
        FileChooser fileChooser = new FileChooser();
        fileChooser.setTitle("Chọn tệp tin cần xác minh");
        File file = fileChooser.showOpenDialog(taVerifyInput.getScene().getWindow());

        if (file != null) {
            selectedVerifyFile = file;
            lblVerifyFilePath.setText(file.getName() + " (" + file.length() + " bytes)");
            addLog("CHỌN FILE KIỂM TRA", "Đã chọn file: " + file.getAbsolutePath());
        }
    }

    @FXML
    private void handleSelectSigFile() {
        FileChooser fileChooser = new FileChooser();
        fileChooser.setTitle("Chọn tệp tin chữ ký .sig");
        fileChooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("Tệp chữ ký (*.sig)", "*.sig"));
        File file = fileChooser.showOpenDialog(taVerifySignature.getScene().getWindow());

        if (file != null) {
            selectedSigFile = file;
            try {
                String sigText = FileService.readStringFromFile(file);
                taVerifySignature.setText(sigText.trim());
                addLog("CHỌN FILE CHỮ KÝ", "Nạp chữ ký số từ file thành công: " + file.getName());
            } catch (Exception e) {
                addLog("LỖI ĐỌC CHỮ KÝ", "Không thể đọc nội dung file chữ ký: " + e.getMessage());
            }
        }
    }

    /**
     * Sự kiện nút "Xác minh chữ ký".
     * Lấy văn bản/tệp tin, phân tích chữ ký Base64, lấy khóa y và kiểm tra hệ thức toán học.
     */
    @FXML
    private void handleVerify() {
        String sigStr = taVerifySignature.getText().trim();
        String yStr = taVerifyPubY.getText().trim();

        // 1. Kiểm định các ô thông tin bắt buộc phải điền
        if (sigStr.isEmpty()) {
            updateVerifyResultCard("DEFAULT", "❌ Vui lòng cung cấp chữ ký số (nhập tay hoặc nạp file .sig)");
            return;
        }
        if (yStr.isEmpty()) {
            updateVerifyResultCard("DEFAULT", "❌ Vui lòng cung cấp khóa công khai y (hệ 10)");
            return;
        }

        // Lấy p và alpha từ Bước 1
        String pStr = taKeyP.getText().trim();
        String alphaStr = taKeyAlpha.getText().trim();

        if (pStr.isEmpty() || alphaStr.isEmpty()) {
            updateVerifyResultCard("DEFAULT", "❌ Thiếu thông tin hệ thống (p, alpha) tại Bước 1.");
            addLog("LỖI XÁC MINH", "Thao tác lỗi: Thiếu tham số chung p và alpha từ Bước 1.");
            return;
        }

        // 2. Chuyển đổi dữ liệu và xử lý định dạng chữ ký bằng ValidationService
        final SignatureData signature;
        final BigInteger p;
        final BigInteger alpha;
        final BigInteger y;

        try {
            // Giải nén cấu trúc chữ ký Base64, bắt lỗi định dạng nếu nhập sai
            signature = ValidationService.parseSignature(sigStr);
        } catch (IllegalArgumentException e) {
            updateVerifyResultCard("INVALID_FORMAT", "❌ Lỗi định dạng chữ ký\n" + e.getMessage());
            addLog("LỖI XÁC MINH", "Xác minh thất bại: Chữ ký sai định dạng (Test 3).");
            return;
        }

        try {
            p = new BigInteger(pStr);
            alpha = new BigInteger(alphaStr);
            y = new BigInteger(yStr);
        } catch (NumberFormatException e) {
            updateVerifyResultCard("DEFAULT", "❌ Khóa công khai y, p, alpha phải là số nguyên hệ 10.");
            return;
        }

        // 3. Quy đổi dữ liệu văn bản/file gốc cần kiểm tra ra mảng byte
        final byte[] dataToVerify;
        final String sourceDescription;

        if (cbVerifySourceType.getValue().equals(SOURCE_FILE)) {
            if (selectedVerifyFile == null) {
                updateVerifyResultCard("DEFAULT", "❌ Vui lòng chọn tệp tin cần xác minh");
                return;
            }
            try {
                dataToVerify = FileService.readBytesFromFile(selectedVerifyFile);
                sourceDescription = "tệp tin nhị phân [" + selectedVerifyFile.getName() + "]";
            } catch (Exception e) {
                updateVerifyResultCard("DEFAULT", "❌ Lỗi không thể đọc tệp tin xác minh");
                return;
            }
        } else {
            String text = taVerifyInput.getText();
            dataToVerify = text.getBytes(StandardCharsets.UTF_8);
            sourceDescription = "văn bản trực tiếp";
        }

        // Vô hiệu hóa nút và chạy ngầm
        btnVerify.setDisable(true);
        progressVerify.setVisible(true);
        addLog("XÁC MINH", "Đang tính toán modular pow và xác thực chữ ký cho " + sourceDescription + "...");

        Task<Boolean> task = new Task<>() {
            @Override
            protected Boolean call() {
                // Thuật toán kiểm định modular
                return model.xacMinhChuKy(dataToVerify, signature, p, alpha, y);
            }
        };

        task.setOnSucceeded(e -> {
            boolean isValid = task.getValue();

            btnVerify.setDisable(false);
            progressVerify.setVisible(false);

            if (isValid) {
                updateVerifyResultCard("VALID", "✅ Hợp lệ\nChữ ký chính xác và văn bản chưa bị sửa đổi.");
                addLog("XÁC MINH THÀNH CÔNG", "✅ Chữ ký HỢP LỆ trên " + sourceDescription + ".");
            } else {
                updateVerifyResultCard("INVALID", "❌ Chữ ký không hợp lệ\nHoặc văn bản đã bị sửa đổi / sai khóa y.");
                addLog("XÁC MINH THẤT BẠI", "❌ Chữ ký KHÔNG HỢP LỆ hoặc tệp tin đã bị sửa đổi.");
            }
        });

        task.setOnFailed(e -> {
            btnVerify.setDisable(false);
            progressVerify.setVisible(false);

            Throwable ex = task.getException();
            updateVerifyResultCard("DEFAULT", "❌ Lỗi tính toán toán học: " + ex.getMessage());
            addLog("LỖI XÁC MINH", "Lỗi phép toán modular: " + ex.getMessage());
        });

        new Thread(task).start();
    }

    /**
     * Đồng bộ hóa màu sắc hiển thị của Thẻ Kết Quả theo trạng thái
     */
    private void updateVerifyResultCard(String status, String message) {
        lblVerifyResult.setText(message);
        vboxResultCard.getStyleClass().clear();

        switch (status) {
            case "VALID":
                vboxResultCard.getStyleClass().addAll("vbox", "result-card-valid");
                break;
            case "INVALID":
            case "INVALID_FORMAT":
                vboxResultCard.getStyleClass().addAll("vbox", "result-card-invalid");
                break;
            default:
                vboxResultCard.getStyleClass().addAll("vbox", "result-card-default");
                break;
        }
    }

    // ==========================================
    // PHẦN XỬ LÝ: DƯỚI CÙNG - LỊCH SỬ LOG & RESET
    // ==========================================

    /**
     * Ghi thêm log vào danh sách có chèn mốc thời gian thực
     */
    private void addLog(String actionType, String message) {
        String timestamp = LocalTime.now().format(timeFormatter);
        String logEntry = String.format("[%s] [%s] %s", timestamp, actionType, message);
        
        // Thêm log chạy an toàn trên luồng UI của JavaFX
        javafx.application.Platform.runLater(() -> {
            lvLogs.getItems().add(logEntry);
            lvLogs.scrollTo(lvLogs.getItems().size() - 1); // Tự động cuộn xuống cuối
        });
    }

    /**
     * Sự kiện nút "Xóa lịch sử". Bắt buộc hiển thị hộp thoại xác nhận.
     */
    @FXML
    private void handleClearLog() {
        Alert alert = new Alert(Alert.AlertType.CONFIRMATION);
        alert.setTitle("Xác nhận xóa dữ liệu");
        alert.setHeaderText("Xóa lịch sử hoạt động");
        alert.setContentText("Bạn có chắc chắn muốn xóa sạch toàn bộ log lịch sử hệ thống hiện tại không?");

        Optional<ButtonType> result = alert.showAndWait();
        if (result.isPresent() && result.get() == ButtonType.OK) {
            lvLogs.getItems().clear();
            addLog("HỆ THỐNG", "Đã xóa lịch sử hoạt động.");
        }
    }

    /**
     * Sự kiện nút "Reset toàn bộ hệ thống". Bắt buộc hiển thị hộp thoại xác nhận.
     * Làm sạch toàn bộ các vùng hiển thị, file đã chọn và đưa hệ thống về ban đầu.
     */
    @FXML
    private void handleResetAll() {
        Alert alert = new Alert(Alert.AlertType.CONFIRMATION);
        alert.setTitle("Xác nhận khôi phục");
        alert.setHeaderText("Thiết lập lại toàn bộ hệ thống");
        alert.setContentText("Hành động này sẽ xóa sạch toàn bộ khóa đã sinh, chữ ký số, văn bản đang nhập và tệp tin đã chọn. Bạn có muốn tiếp tục?");

        Optional<ButtonType> result = alert.showAndWait();
        if (result.isPresent() && result.get() == ButtonType.OK) {
            
            // 1. Reset Cột 1
            taKeyP.clear();
            taKeyAlpha.clear();
            taKeyX.clear();
            taKeyY.clear();
            tfManualP.clear();
            tfManualAlpha.clear();
            tfManualX.clear();
            hideErrorLabel(lblErrorP);
            hideErrorLabel(lblErrorAlpha);
            hideErrorLabel(lblErrorX);
            currentKeyPair = null;
            if (vboxManualInput.isVisible()) {
                toggleManualMode(); // Ẩn bảng manual
            }

            // 2. Reset Cột 2
            taSignInput.clear();
            selectedSignFile = null;
            lblSignFilePath.setText("Chưa chọn file nào.");
            taSignOutput.clear();
            cbSignSourceType.setValue(SOURCE_TEXT);

            // 3. Reset Cột 3
            taVerifyInput.clear();
            selectedVerifyFile = null;
            lblVerifyFilePath.setText("Chưa chọn file nào.");
            taVerifySignature.clear();
            selectedSigFile = null;
            taVerifyPubY.clear();
            cbVerifySourceType.setValue(SOURCE_TEXT);
            updateVerifyResultCard("DEFAULT", "Chưa thực hiện xác minh");

            // 4. Xóa log và ghi log khởi tạo
            lvLogs.getItems().clear();
            addLog("RESET", "Hệ thống đã được khôi phục thành công về trạng thái mặc định.");
        }
    }
}

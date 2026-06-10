package vn.haui.elgamal.controller;

import javafx.animation.PauseTransition;
import javafx.concurrent.Task;
import javafx.event.ActionEvent;
import javafx.fxml.FXML;
import javafx.scene.control.Alert;
import javafx.scene.control.Button;
import javafx.scene.control.ButtonType;
import javafx.scene.control.ComboBox;
import javafx.scene.control.Label;
import javafx.scene.control.ListView;
import javafx.scene.control.ProgressIndicator;
import javafx.scene.control.TextArea;
import javafx.scene.control.TextField;
import javafx.scene.input.Clipboard;
import javafx.scene.input.ClipboardContent;
import javafx.scene.layout.VBox;
import javafx.stage.FileChooser;
import javafx.util.Duration;
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
import java.util.Arrays;
import java.util.Base64;
import java.util.Optional;

public class MainController {

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
    @FXML private Button btnLoadKeys;
    @FXML private Button btnSaveKeys;

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

    @FXML private Button btnClearLog;
    @FXML private Button btnResetAll;
    @FXML private ListView<String> lvLogs;

    private ElGamalModel model;
    private KeyPairData currentKeyPair;
    private File selectedSignFile;
    private File selectedVerifyFile;
    private File selectedSigFile;
    private byte[] lastSignedData;
    private String lastSignedSignature;

    private static final String SOURCE_TEXT = "Văn bản trực tiếp";
    private static final String SOURCE_FILE = "Chọn file tài liệu";
    private final DateTimeFormatter timeFormatter = DateTimeFormatter.ofPattern("HH:mm:ss");

    @FXML
    public void initialize() {
        model = new ElGamalModel();

        cbBitLength.getItems().addAll(512, 1024);
        cbBitLength.setValue(512);

        cbSignSourceType.getItems().addAll(SOURCE_TEXT, SOURCE_FILE);
        cbSignSourceType.setValue(SOURCE_TEXT);

        cbVerifySourceType.getItems().addAll(SOURCE_TEXT, SOURCE_FILE);
        cbVerifySourceType.setValue(SOURCE_TEXT);

        tfManualP.textProperty().addListener((obs, oldVal, newVal) -> validateManualP());
        tfManualAlpha.textProperty().addListener((obs, oldVal, newVal) -> validateManualAlpha());
        tfManualX.textProperty().addListener((obs, oldVal, newVal) -> validateManualX());

        taKeyY.textProperty().addListener((obs, oldVal, newVal) -> {
            if (newVal != null && !newVal.trim().isEmpty()) {
                taVerifyPubY.setText(newVal.trim());
            }
        });

        handleSignSourceTypeChange();
        handleVerifySourceTypeChange();
        updateVerifyResultCard("DEFAULT", "Chưa thực hiện xác minh");
        addLog("HỆ THỐNG", "Chương trình khởi chạy thành công. Bit-length mặc định: 512-bit.");
    }

    @FXML
    private void handleAutoKeyGen() {
        int bitLength = cbBitLength.getValue();
        setBusy(btnAutoKey, progressKeyGen, true);
        btnManualMode.setDisable(true);
        addLog("SINH KHÓA", "Bắt đầu sinh khóa ElGamal " + bitLength + "-bit...");

        Task<KeyPairData> task = new Task<>() {
            @Override
            protected KeyPairData call() {
                return model.sinhKhoaTuDong(bitLength);
            }
        };

        task.setOnSucceeded(e -> {
            currentKeyPair = task.getValue();
            populateKeyFields(currentKeyPair);
            setBusy(btnAutoKey, progressKeyGen, false);
            btnManualMode.setDisable(false);
            addLog("SINH KHÓA", "Sinh khóa thành công trong " + model.getLastOperationTimeMs() + " ms.");
            showInfo("Sinh khóa thành công", null, "Đã tạo cặp khóa ElGamal " + bitLength + "-bit.");
        });

        task.setOnFailed(e -> {
            setBusy(btnAutoKey, progressKeyGen, false);
            btnManualMode.setDisable(false);
            Throwable ex = task.getException();
            showError("Lỗi sinh khóa", null, ex == null ? "Không xác định." : ex.getMessage());
            addLog("LỖI SINH KHÓA", ex == null ? "Không xác định." : ex.getMessage());
        });

        new Thread(task).start();
    }

    @FXML
    private void toggleManualMode() {
        boolean visible = vboxManualInput.isVisible();
        vboxManualInput.setVisible(!visible);
        vboxManualInput.setManaged(!visible);
        btnManualMode.setText(visible ? "Nhập thủ công" : "Ẩn nhập thủ công");
        addLog("CHẾ ĐỘ KHÓA", visible ? "Đóng bảng nhập tay." : "Mở bảng nhập tay.");
    }

    @FXML
    private void handleApplyManualKey() {
        boolean valid = validateManualP() & validateManualAlpha() & validateManualX();
        if (!valid) {
            showError("Lỗi nhập khóa", null, "Các tham số p, alpha, x chưa hợp lệ.");
            addLog("LỖI NHẬP KHÓA", "Các tham số chưa vượt qua kiểm định.");
            return;
        }

        try {
            BigInteger p = new BigInteger(tfManualP.getText().trim());
            BigInteger alpha = new BigInteger(tfManualAlpha.getText().trim());
            BigInteger x = new BigInteger(tfManualX.getText().trim());
            currentKeyPair = model.sinhKhoaTuChon(p, alpha, x);
            populateKeyFields(currentKeyPair);
            addLog("NHẬP KHÓA TAY", "Áp dụng thành công khóa nhập tay.");
            showInfo("Áp dụng khóa thành công", null, "Đã tạo khóa công khai y từ bộ tham số nhập tay.");
        } catch (Exception e) {
            showError("Lỗi nhập khóa", null, e.getMessage());
            addLog("LỖI NHẬP KHÓA", e.getMessage());
        }
    }

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
            showErrorLabel(lblErrorP, "❌ p phải là số nguyên hợp lệ.");
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
        if (textP.isEmpty() || !validateManualP()) {
            showErrorLabel(lblErrorAlpha, "❌ Vui lòng nhập p hợp lệ trước.");
            return false;
        }
        try {
            BigInteger p = new BigInteger(textP);
            BigInteger alpha = new BigInteger(textAlpha);
            if (!ValidationService.isPrimitiveRoot(alpha, p)) {
                showErrorLabel(lblErrorAlpha, "❌ alpha không phải căn nguyên thủy modulo p.");
                return false;
            }
            if (ValidationService.isHeuristicPrimitiveRootCheck(p)) {
                showWarningLabel(lblErrorAlpha, "⚠️ Kiểm tra alpha đang ở chế độ heuristic.");
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
            showErrorLabel(lblErrorX, "❌ Vui lòng nhập p hợp lệ trước.");
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
        label.setStyle("-fx-text-fill: #dc2626;");
        label.setManaged(true);
        label.setVisible(true);
    }

    private void showWarningLabel(Label label, String message) {
        label.setText(message);
        label.setStyle("-fx-text-fill: #d97706;");
        label.setManaged(true);
        label.setVisible(true);
    }

    private void hideErrorLabel(Label label) {
        label.setManaged(false);
        label.setVisible(false);
    }

    @FXML private void copyP(ActionEvent event) { copyToClipboard(taKeyP.getText(), "p", (Button) event.getSource()); }
    @FXML private void copyAlpha(ActionEvent event) { copyToClipboard(taKeyAlpha.getText(), "alpha", (Button) event.getSource()); }
    @FXML private void copyX(ActionEvent event) { copyToClipboard(taKeyX.getText(), "x", (Button) event.getSource()); }
    @FXML private void copyY(ActionEvent event) { copyToClipboard(taKeyY.getText(), "y", (Button) event.getSource()); }

    private void copyToClipboard(String content, String name, Button button) {
        if (content == null || content.trim().isEmpty()) {
            showError("Lỗi copy", null, "Không có dữ liệu " + name + " để copy.");
            addLog("COPY LỖI", "Không có dữ liệu " + name + " để copy.");
            return;
        }

        Clipboard clipboard = Clipboard.getSystemClipboard();
        ClipboardContent clipboardContent = new ClipboardContent();
        clipboardContent.putString(content.trim());
        clipboard.setContent(clipboardContent);
        addLog("COPY", "Đã copy giá trị " + name + " vào Clipboard.");

        if (button != null) {
            String oldText = button.getText();
            String oldStyle = button.getStyle();
            button.setText("Đã copy!");
            button.setStyle("-fx-background-color: #10b981; -fx-text-fill: white;");
            PauseTransition pause = new PauseTransition(Duration.seconds(1));
            pause.setOnFinished(e -> {
                button.setText(oldText);
                button.setStyle(oldStyle);
            });
            pause.play();
        }
    }

    @FXML
    private void handleLoadKeyFromFile() {
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Chọn tệp tin khóa ElGamal (.txt)");
        chooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("Tệp văn bản (*.txt)", "*.txt"));
        File file = chooser.showOpenDialog(taKeyP.getScene().getWindow());
        if (file == null) {
            return;
        }
        try {
            currentKeyPair = FileService.readKeyFile(file);
            populateKeyFields(currentKeyPair);
            if (currentKeyPair.hasPrivateKey()) {
                addLog("NẠP KHÓA", "Đã nạp đầy đủ cặp khóa từ file: " + file.getName());
                showInfo("Nạp khóa thành công", null, "Đã nạp đầy đủ cặp khóa từ file.");
            } else {
                addLog("NẠP KHÓA", "Đã nạp khóa công khai từ file: " + file.getName());
                showInfo("Nạp khóa thành công", null, "Đây là khóa công khai. Bạn chỉ có thể xác minh, không thể ký.");
            }
        } catch (Exception e) {
            showError("Lỗi nạp khóa", "Không thể đọc thông tin khóa", e.getMessage());
            addLog("LỖI NẠP KHÓA", e.getMessage());
        }
    }

    @FXML
    private void handleSaveKeys() {
        if (currentKeyPair == null) {
            showError("Lỗi lưu khóa", null, "Không có thông tin khóa để lưu.");
            addLog("LƯU KHÓA THẤT BẠI", "Không có thông tin khóa để lưu.");
            return;
        }
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Lưu tệp tin khóa ElGamal");
        chooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("Tệp văn bản (*.txt)", "*.txt"));
        File file = chooser.showSaveDialog(taKeyP.getScene().getWindow());
        if (file == null) {
            return;
        }
        try {
            FileService.writeKeyFile(file, currentKeyPair);
            addLog("LƯU KHÓA", "Đã lưu khóa vào file: " + file.getName());
            showInfo("Lưu thành công", null, "Đã lưu thông tin khóa vào tệp: " + file.getName());
        } catch (Exception e) {
            showError("Lỗi lưu khóa", null, e.getMessage());
            addLog("LỖI LƯU KHÓA", e.getMessage());
        }
    }

    @FXML
    private void handleSignSourceTypeChange() {
        boolean isFile = SOURCE_FILE.equals(cbSignSourceType.getValue());
        vboxSignText.setVisible(!isFile);
        vboxSignText.setManaged(!isFile);
        vboxSignFile.setVisible(isFile);
        vboxSignFile.setManaged(isFile);
        addLog("NGUỒN KÝ", isFile ? "Đã chuyển sang chế độ ký tệp tin." : "Đã chuyển sang chế độ ký văn bản trực tiếp.");
    }

    @FXML
    private void handleSelectSignFile() {
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Chọn tệp tin tài liệu để ký số");
        File file = chooser.showOpenDialog(taSignInput.getScene().getWindow());
        if (file != null) {
            selectedSignFile = file;
            lblSignFilePath.setText(file.getName() + " (" + file.length() + " bytes)");
            addLog("CHỌN FILE KÝ", "Đã chọn file: " + file.getAbsolutePath());
        }
    }

    private byte[] getSignInputData() throws Exception {
        if (SOURCE_FILE.equals(cbSignSourceType.getValue())) {
            if (selectedSignFile == null) {
                throw new IllegalStateException("Chưa chọn tệp tin để ký.");
            }
            return FileService.readBytesFromFile(selectedSignFile);
        }
        String text = taSignInput.getText();
        if (text == null || text.trim().isEmpty()) {
            throw new IllegalStateException("Nội dung văn bản để ký đang trống.");
        }
        ValidationService.validateTextLength(text);
        return text.getBytes(StandardCharsets.UTF_8);
    }

    private boolean requireCurrentPrivateKeyForSign() {
        if (currentKeyPair == null) {
            showError("Lỗi khóa", null, "Chưa có cặp khóa. Vui lòng tạo hoặc nạp khóa trước khi ký.");
            addLog("LỖI KÝ SỐ", "Chưa có cặp khóa.");
            return false;
        }
        if (!currentKeyPair.hasPrivateKey()) {
            showError("Lỗi khóa", null, "Khóa hiện tại chỉ có public key. Không thể ký nếu thiếu private key x.");
            addLog("LỖI KÝ SỐ", "Khóa hiện tại không chứa private key x.");
            return false;
        }
        return true;
    }

    @FXML
    private void handleSign() {
        if (!requireCurrentPrivateKeyForSign()) {
            return;
        }

        final byte[] dataToSign;
        final String sourceDescription;
        try {
            dataToSign = getSignInputData();
            sourceDescription = SOURCE_FILE.equals(cbSignSourceType.getValue())
                    ? "tệp tin [" + selectedSignFile.getName() + "]"
                    : "văn bản trực tiếp";
        } catch (Exception e) {
            showError("Lỗi dữ liệu ký", null, e.getMessage());
            addLog("LỖI ĐỌC DỮ LIỆU", e.getMessage());
            return;
        }

        setBusy(btnSign, progressSign, true);
        addLog("KÝ SỐ", "Đang tạo chữ ký số cho " + sourceDescription + "...");

        Task<SignatureData> task = new Task<>() {
            @Override
            protected SignatureData call() {
                return model.kyVanBan(dataToSign, currentKeyPair);
            }
        };

        task.setOnSucceeded(e -> {
            SignatureData sig = task.getValue();
            String rBase64 = Base64.getEncoder().encodeToString(sig.getR().toByteArray());
            String sBase64 = Base64.getEncoder().encodeToString(sig.getS().toByteArray());
            String output = rBase64 + "|" + sBase64;
            taSignOutput.setText(output);
            lastSignedData = Arrays.copyOf(dataToSign, dataToSign.length);
            lastSignedSignature = output;
            setBusy(btnSign, progressSign, false);
            addLog("KÝ SỐ", "Ký số thành công trong " + model.getLastOperationTimeMs() + " ms.");
            showInfo("Ký thành công", null, "Đã tạo chữ ký số ElGamal cho dữ liệu đầu vào.");
        });

        task.setOnFailed(e -> {
            setBusy(btnSign, progressSign, false);
            Throwable ex = task.getException();
            showError("Lỗi ký số", null, ex == null ? "Không xác định." : ex.getMessage());
            addLog("LỖI KÝ SỐ", ex == null ? "Không xác định." : ex.getMessage());
        });

        new Thread(task).start();
    }

    @FXML
    private void handleSaveSignature() {
        String sigStr = taSignOutput.getText() == null ? "" : taSignOutput.getText().trim();
        if (sigStr.isEmpty()) {
            showError("Lỗi lưu chữ ký", null, "Chưa có chữ ký số để lưu.");
            addLog("LƯU CHỮ KÝ THẤT BẠI", "Chưa có chữ ký số để lưu.");
            return;
        }
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Lưu tệp tin chữ ký (.sig)");
        chooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("Tệp chữ ký (*.sig)", "*.sig"));
        File file = chooser.showSaveDialog(taSignOutput.getScene().getWindow());
        if (file == null) {
            return;
        }
        try {
            FileService.writeSignatureFile(file, sigStr);
            addLog("LƯU CHỮ KÝ", "Đã lưu chữ ký vào file: " + file.getName());
            showInfo("Lưu thành công", null, "Đã lưu chữ ký số vào tệp: " + file.getName());
        } catch (Exception e) {
            showError("Lỗi lưu chữ ký", null, e.getMessage());
            addLog("LỖI LƯU CHỮ KÝ", e.getMessage());
        }
    }

    @FXML
    private void handleVerifySourceTypeChange() {
        boolean isFile = SOURCE_FILE.equals(cbVerifySourceType.getValue());
        vboxVerifyText.setVisible(!isFile);
        vboxVerifyText.setManaged(!isFile);
        vboxVerifyFile.setVisible(isFile);
        vboxVerifyFile.setManaged(isFile);
        addLog("NGUỒN XÁC MINH", isFile ? "Đã chuyển sang chế độ xác minh tệp tin." : "Đã chuyển sang chế độ xác minh văn bản trực tiếp.");
    }

    @FXML
    private void handleSelectVerifyFile() {
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Chọn tệp tin cần xác minh");
        File file = chooser.showOpenDialog(taVerifyInput.getScene().getWindow());
        if (file != null) {
            selectedVerifyFile = file;
            lblVerifyFilePath.setText(file.getName() + " (" + file.length() + " bytes)");
            addLog("CHỌN FILE KIỂM TRA", "Đã chọn file: " + file.getAbsolutePath());
        }
    }

    @FXML
    private void handleSelectSigFile() {
        FileChooser chooser = new FileChooser();
        chooser.setTitle("Chọn tệp tin chữ ký .sig");
        chooser.getExtensionFilters().add(new FileChooser.ExtensionFilter("Tệp chữ ký (*.sig)", "*.sig"));
        File file = chooser.showOpenDialog(taVerifySignature.getScene().getWindow());
        if (file != null) {
            selectedSigFile = file;
            try {
                String sig = FileService.readStringFromFile(file);
                taVerifySignature.setText(sig.trim());
                addLog("CHỌN FILE CHỮ KÝ", "Đã nạp file chữ ký: " + file.getName());
            } catch (Exception e) {
                showError("Lỗi đọc chữ ký", null, e.getMessage());
                addLog("LỖI ĐỌC CHỮ KÝ", e.getMessage());
            }
        }
    }

    private byte[] getVerifyInputData() throws Exception {
        if (SOURCE_FILE.equals(cbVerifySourceType.getValue())) {
            if (selectedVerifyFile == null) {
                throw new IllegalStateException("Vui lòng chọn tệp tin cần xác minh.");
            }
            return FileService.readBytesFromFile(selectedVerifyFile);
        }

        String text = taVerifyInput.getText();
        if (text == null || text.trim().isEmpty()) {
            throw new IllegalStateException("Vui lòng nhập văn bản cần xác minh.");
        }
        ValidationService.validateTextLength(text);
        return text.getBytes(StandardCharsets.UTF_8);
    }

    private boolean requireSystemParamsForVerify() {
        String pStr = taKeyP.getText() == null ? "" : taKeyP.getText().trim();
        String alphaStr = taKeyAlpha.getText() == null ? "" : taKeyAlpha.getText().trim();
        if (pStr.isEmpty() || alphaStr.isEmpty()) {
            updateVerifyResultCard("DEFAULT", "❌ Thiếu thông tin hệ thống p và alpha ở Bước 1.");
            addLog("LỖI XÁC MINH", "Thiếu p hoặc alpha.");
            return false;
        }
        return true;
    }

    @FXML
    private void handleVerify() {
        String sigStr = taVerifySignature.getText() == null ? "" : taVerifySignature.getText().trim();
        String yStr = taVerifyPubY.getText() == null ? "" : taVerifyPubY.getText().trim();

        if (sigStr.isEmpty()) {
            updateVerifyResultCard("DEFAULT", "❌ Vui lòng cung cấp chữ ký số.");
            return;
        }
        if (yStr.isEmpty()) {
            updateVerifyResultCard("DEFAULT", "❌ Vui lòng cung cấp khóa công khai y.");
            return;
        }
        if (!requireSystemParamsForVerify()) {
            return;
        }

        final SignatureData signature;
        final BigInteger p;
        final BigInteger alpha;
        final BigInteger y;

        try {
            signature = ValidationService.parseSignature(sigStr);
        } catch (IllegalArgumentException e) {
            updateVerifyResultCard("INVALID_FORMAT", "❌ Lỗi định dạng chữ ký\n" + e.getMessage());
            addLog("LỖI XÁC MINH", "Chữ ký sai định dạng.");
            return;
        }

        try {
            p = new BigInteger(taKeyP.getText().trim());
            alpha = new BigInteger(taKeyAlpha.getText().trim());
            y = new BigInteger(yStr);
        } catch (NumberFormatException e) {
            updateVerifyResultCard("DEFAULT", "❌ p, alpha, y phải là số nguyên hệ 10.");
            return;
        }

        BigInteger r = signature.getR();
        BigInteger s = signature.getS();
        BigInteger pm1 = p.subtract(BigInteger.ONE);
        if (y.compareTo(BigInteger.ONE) <= 0 || y.compareTo(p) >= 0) {
            updateVerifyResultCard("INVALID", "❌ Sai khóa công khai y\nGiá trị y không nằm trong khoảng hợp lệ.");
            return;
        }
        if (r.compareTo(BigInteger.ZERO) <= 0 || r.compareTo(p) >= 0) {
            updateVerifyResultCard("INVALID", "❌ Sai chữ ký số\nThành phần r không hợp lệ.");
            return;
        }
        if (s.compareTo(BigInteger.ZERO) <= 0 || s.compareTo(pm1) >= 0) {
            updateVerifyResultCard("INVALID", "❌ Sai chữ ký số\nThành phần s không hợp lệ.");
            return;
        }

        final byte[] dataToVerify;
        final String sourceDescription;
        try {
            dataToVerify = getVerifyInputData();
            sourceDescription = SOURCE_FILE.equals(cbVerifySourceType.getValue())
                    ? "tệp tin [" + selectedVerifyFile.getName() + "]"
                    : "văn bản trực tiếp";
        } catch (Exception e) {
            updateVerifyResultCard("DEFAULT", "❌ " + e.getMessage());
            return;
        }

        setBusy(btnVerify, progressVerify, true);
        addLog("XÁC MINH", "Đang xác thực chữ ký cho " + sourceDescription + "...");

        Task<Boolean> task = new Task<>() {
            @Override
            protected Boolean call() {
                return model.xacMinhChuKy(dataToVerify, signature, p, alpha, y);
            }
        };

        task.setOnSucceeded(e -> {
            boolean valid = task.getValue();
            setBusy(btnVerify, progressVerify, false);
            if (valid) {
                updateVerifyResultCard("VALID", "✅ Hợp lệ\nChữ ký chính xác và dữ liệu chưa bị sửa đổi.");
                addLog("XÁC MINH THÀNH CÔNG", "Chữ ký hợp lệ trên " + sourceDescription + ".");
                return;
            }

            if (currentKeyPair != null && !y.equals(currentKeyPair.getY())) {
                updateVerifyResultCard("INVALID", "❌ Sai khóa công khai y\nKhóa y không khớp với khóa hệ thống.");
                addLog("XÁC MINH THẤT BẠI", "Sai khóa công khai y.");
            } else if (lastSignedData != null && !Arrays.equals(dataToVerify, lastSignedData)) {
                updateVerifyResultCard("INVALID", "❌ Văn bản bị sửa đổi\nDữ liệu xác minh không khớp dữ liệu gốc lúc ký.");
                addLog("XÁC MINH THẤT BẠI", "Dữ liệu đã bị sửa đổi.");
            } else if (lastSignedSignature != null && !sigStr.equals(lastSignedSignature)) {
                updateVerifyResultCard("INVALID", "❌ Sai chữ ký số\nChữ ký không khớp với chữ ký đã sinh.");
                addLog("XÁC MINH THẤT BẠI", "Chữ ký đã bị thay đổi.");
            } else {
                updateVerifyResultCard("INVALID", "❌ Xác minh không hợp lệ\nChữ ký, dữ liệu hoặc khóa công khai không khớp.");
                addLog("XÁC MINH THẤT BẠI", "Xác minh không thành công.");
            }
        });

        task.setOnFailed(e -> {
            setBusy(btnVerify, progressVerify, false);
            Throwable ex = task.getException();
            updateVerifyResultCard("DEFAULT", "❌ Lỗi tính toán: " + (ex == null ? "Không xác định." : ex.getMessage()));
            addLog("LỖI XÁC MINH", ex == null ? "Không xác định." : ex.getMessage());
        });

        new Thread(task).start();
    }

    private void updateVerifyResultCard(String status, String message) {
        lblVerifyResult.setText(message);
        vboxResultCard.getStyleClass().removeAll("result-card-valid", "result-card-invalid", "result-card-default");
        switch (status) {
            case "VALID" -> vboxResultCard.getStyleClass().add("result-card-valid");
            case "INVALID", "INVALID_FORMAT" -> vboxResultCard.getStyleClass().add("result-card-invalid");
            default -> vboxResultCard.getStyleClass().add("result-card-default");
        }
    }

    private void addLog(String actionType, String message) {
        String timestamp = LocalTime.now().format(timeFormatter);
        String logEntry = String.format("[%s] [%s] %s", timestamp, actionType, message);
        lvLogs.getItems().add(logEntry);
        lvLogs.scrollTo(lvLogs.getItems().size() - 1);
    }

    private void showError(String title, String header, String content) {
        Alert alert = new Alert(Alert.AlertType.ERROR);
        alert.setTitle(title);
        alert.setHeaderText(header);
        alert.setContentText(content);
        alert.showAndWait();
    }

    private void showInfo(String title, String header, String content) {
        Alert alert = new Alert(Alert.AlertType.INFORMATION);
        alert.setTitle(title);
        alert.setHeaderText(header);
        alert.setContentText(content);
        alert.showAndWait();
    }

    private void setBusy(Button button, ProgressIndicator progress, boolean busy) {
        button.setDisable(busy);
        progress.setVisible(busy);
        progress.setManaged(busy);
    }

    private void populateKeyFields(KeyPairData keyPair) {
        taKeyP.setText(keyPair.getP().toString());
        taKeyAlpha.setText(keyPair.getAlpha().toString());
        taKeyX.setText(keyPair.hasPrivateKey() ? keyPair.getX().toString() : "");
        taKeyY.setText(keyPair.getY().toString());
    }

    @FXML
    private void handleClearLog() {
        Alert alert = new Alert(Alert.AlertType.CONFIRMATION);
        alert.setTitle("Xác nhận xóa dữ liệu");
        alert.setHeaderText("Xóa lịch sử hoạt động");
        alert.setContentText("Bạn có chắc chắn muốn xóa toàn bộ log hiện tại không?");
        Optional<ButtonType> result = alert.showAndWait();
        if (result.isPresent() && result.get() == ButtonType.OK) {
            lvLogs.getItems().clear();
            addLog("HỆ THỐNG", "Đã xóa lịch sử hoạt động.");
        }
    }

    @FXML
    private void handleResetAll() {
        Alert alert = new Alert(Alert.AlertType.CONFIRMATION);
        alert.setTitle("Xác nhận khôi phục");
        alert.setHeaderText("Thiết lập lại toàn bộ hệ thống");
        alert.setContentText("Hành động này sẽ xóa sạch toàn bộ dữ liệu hiện tại. Bạn có muốn tiếp tục?");
        Optional<ButtonType> result = alert.showAndWait();
        if (result.isPresent() && result.get() == ButtonType.OK) {
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
                toggleManualMode();
            }

            taSignInput.clear();
            selectedSignFile = null;
            lblSignFilePath.setText("Chưa chọn file nào.");
            taSignOutput.clear();
            cbSignSourceType.setValue(SOURCE_TEXT);
            lastSignedData = null;
            lastSignedSignature = null;

            taVerifyInput.clear();
            selectedVerifyFile = null;
            lblVerifyFilePath.setText("Chưa chọn file nào.");
            taVerifySignature.clear();
            selectedSigFile = null;
            taVerifyPubY.clear();
            cbVerifySourceType.setValue(SOURCE_TEXT);
            updateVerifyResultCard("DEFAULT", "Chưa thực hiện xác minh");

            lvLogs.getItems().clear();
            addLog("RESET", "Hệ thống đã được khôi phục về trạng thái mặc định.");
        }
    }
}
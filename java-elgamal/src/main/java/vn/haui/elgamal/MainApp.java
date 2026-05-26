package vn.haui.elgamal;

import javafx.application.Application;
import javafx.fxml.FXMLLoader;
import javafx.scene.Parent;
import javafx.scene.Scene;
import javafx.stage.Stage;

import java.io.IOException;

/**
 * Lớp khởi chạy chính của ứng dụng Chữ Ký Số ElGamal.
 * Kế thừa từ javafx.application.Application để quản lý vòng đời ứng dụng JavaFX.
 */
public class MainApp extends Application {

    @Override
    public void start(Stage primaryStage) {
        try {
            // Tải tệp tin thiết kế giao diện FXML
            FXMLLoader loader = new FXMLLoader(getClass().getResource("/vn/haui/elgamal/fxml/MainView.fxml"));
            Parent root = loader.load();

            // Khởi tạo màn hình chính (Scene) với kích thước yêu cầu 1200x700
            Scene scene = new Scene(root, 1200, 700);
            
            // Liên kết tệp tin cấu hình giao diện CSS
            if (getClass().getResource("/vn/haui/elgamal/css/style.css") != null) {
                String cssPath = getClass().getResource("/vn/haui/elgamal/css/style.css").toExternalForm();
                scene.getStylesheets().add(cssPath);
            } else {
                System.err.println("Không tìm thấy tệp tin style.css");
            }

            // Thiết lập các thuộc tính cho Stage (Cửa sổ chính)
            primaryStage.setTitle("Chữ Ký Số ElGamal");
            primaryStage.setScene(scene);
            primaryStage.setMinWidth(1100); // Ràng buộc chiều rộng tối thiểu như yêu cầu
            primaryStage.setMinHeight(600); // Chiều cao tối thiểu hợp lý
            primaryStage.show();
        } catch (IOException e) {
            e.printStackTrace();
            System.err.println("Gặp lỗi trong quá trình tải giao diện MainView.fxml: " + e.getMessage());
        }
    }

    /**
     * Phương thức khởi chạy chương trình từ JVM.
     */
    public static void main(String[] args) {
        launch(args);
    }
}

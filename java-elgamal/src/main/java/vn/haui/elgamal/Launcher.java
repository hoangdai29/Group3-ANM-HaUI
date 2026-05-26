package vn.haui.elgamal;

/**
 * Lớp khởi chạy trung gian (Launcher Wrapper) cho ứng dụng JavaFX.
 * 
 * Lớp này KHÔNG kế thừa từ javafx.application.Application. Do đó, JVM sẽ khởi chạy
 * ứng dụng ở chế độ classpath thông thường, giúp bỏ qua lỗi "JavaFX runtime components are missing"
 * mà không cần phải cấu hình phức tạp các tham số VM Options --module-path trong IntelliJ IDEA.
 */
public class Launcher {
    public static void main(String[] args) {
        // Chuyển tiếp luồng chạy sang phương thức main thực sự của MainApp
        MainApp.main(args);
    }
}

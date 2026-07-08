# Kiến trúc UI (Procedural UI Architecture)

Thư mục này chứa toàn bộ các Script liên quan đến Giao diện người dùng (UI) của dự án.
Đặc biệt, dự án này sử dụng kiến trúc **Procedural UI (Sinh UI bằng code)** thay vì thiết kế thủ công trên Hierarchy (như Prefab).

## Cấu trúc thư mục

* **Core/**: Chứa các script nền tảng dùng chung cho tất cả các giao diện.
  * `UIButtonVibe.cs`: Script tự động gắn vào các nút bấm để tạo hiệu ứng Scale nhún nhảy và âm thanh Click.

* **MainMenu/**: Các thành phần của màn hình chính (Main Menu). Sử dụng kỹ thuật Partial Classes để chia nhỏ file controller khổng lồ.
  * `MainMenuController.cs`: Lớp chính, xử lý State và chuyển trang.
  * `MainMenuController.UIBuilder.cs`: Bộ công cụ (Helpers) tạo các thành phần UI cơ bản (Button, Text, Image, Shadow...).
  * `MainMenuController.Home.cs`, `ModeSelect.cs`, `Lobby.cs`, `OfflineMap.cs`: Giao diện của từng trang tương ứng.

* **StartScreen/**: Màn hình khởi động lúc mới vào game (Vibe Sa Mạc / Arcade).
  * `StartScreenController.cs`: Controller chính của StartScreen, cũng sinh UI bằng code.
  * `StartMenuSlot.cs`: Script cho các slot menu.

* **Game/**: Giao diện In-game (trong lúc đang chơi).
  * `GameUIHandler.cs`: Quản lý hiển thị máu, đạn, thời gian...
  * `PauseMenu.cs`: Menu tạm dừng game.

* **MobileControl/**: Các thành phần UI điều khiển trên thiết bị di động (JoyStick, Button Shoot).

## Nguyên tắc dành cho AI (AI Rules)

1. **KHÔNG sửa UI Prefab**: Giao diện được sinh ra bằng code (`MainMenuController` và `StartScreenController`). Nếu bạn muốn thay đổi UI, **bạn phải sửa code sinh UI**, không được yêu cầu user kéo thả trong Unity Editor.
2. **Sử dụng UIButtonVibe**: Mọi nút bấm mới sinh ra cần gắn component `UIButtonVibe` để đồng bộ trải nghiệm UX/Audio.
3. **Màu sắc chuẩn**: Các màu sắc chủ đạo được định nghĩa sẵn trong `MainMenuController.cs` (Cam, Xanh lơ, Xám đen). Hãy tái sử dụng chúng để giữ vững tính nhất quán của thiết kế.

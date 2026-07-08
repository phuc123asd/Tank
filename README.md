# TANKS ARENA - Tài liệu chi tiết Đấu trường Sa mạc (Desert Arena)

Dự án này là một game bắn xe tăng đấu trường nhiều người chơi (Top-down Arena Tank Game) được phát triển bằng **Unity**, sử dụng **Universal Render Pipeline (URP)**, hệ thống **Input System mới** và **NavMesh** để điều khiển AI.

Tài liệu này mô tả chi tiết cấu trúc dự án và tập trung phân tích sâu vào màn chơi **Sa mạc (Desert Screen)** thông qua cảnh [Desert.unity](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scenes/Desert.unity).

---

## 1. Luồng chạy chính của Game (Game Flow)

1. **Màn hình bắt đầu**: Cảnh [Start.unity](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scenes/Start.unity) (quản lý bởi [StartScreenController.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/UI/StartScreenController.cs)) hiển thị tiêu đề và yêu cầu người chơi ấn phím bất kỳ để tiếp tục.
2. **Menu chính**: Cảnh [MainMenu.unity](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scenes/MainMenu.unity) (quản lý bởi [MainMenuController.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/UI/MainMenuController.cs)) cho phép lựa chọn 1 trong 3 bản đồ chiến đấu:
   * **DESERT** (Đấu trường Sa mạc)
   * **JUNGLE** (Đấu trường Rừng rậm)
   * **MOON** (Đấu trường Mặt trăng)
3. **Phòng chờ (Lobby) trong trận**: Khi tải cảnh [Desert.unity](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scenes/Desert.unity), giao diện UI Lobby hiển thị cho phép thiết lập từ 2 đến 4 xe tăng chiến đấu. Người chơi có thể gán xe tăng cho **Player 1** (sử dụng phím bên trái hoặc Gamepad/Virtual Joystick), **Player 2** (sử dụng phím bên phải), hoặc **Computer** (AI điều khiển), đồng thời chọn màu sắc đại diện cho xe tăng.
4. **Trận đấu**: Diễn ra theo các vòng đấu (Rounds) liên tục. Xe tăng cuối cùng còn sống sót sẽ thắng vòng đấu đó. Người chơi đạt đủ số vòng thắng yêu cầu (mặc định là 5 rounds) sẽ giành chiến thắng chung cuộc và game tự động quay lại Main Menu.

---

## 2. Chi tiết Đấu trường Sa mạc (Desert Screen)

Bản đồ Sa mạc được thiết kế dưới dạng một đấu trường cát vàng hoang dã kết hợp các công trình công nghiệp khai thác dầu và căn cứ quân sự bỏ hoang. Bản đồ sử dụng tài nguyên môi trường dạng Low-Poly chất lượng cao được lưu trữ trong thư mục prefab [Desert](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Prefabs/Environment/Desert).

### 2.1. Cấu trúc phân cấp cảnh (Scene Hierarchy) của `LevelDesert`

Dưới GameObject gốc là prefab [LevelDesert.prefab](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Prefabs/Levels/LevelDesert.prefab), cảnh được tổ chức thành các nhóm đối tượng cụ thể:

* **Boundaries (Ranh giới bản đồ)**:
  * Gồm 4 vách đá sa mạc lớn (`CliffSand`) nằm ở các phía, chứa hệ thống Collider hỗn hợp (Box, Capsule, Sphere) tạo thành tường chắn vật lý không cho xe tăng đi lệch khỏi khu vực thi đấu.
* **Ground (Mặt đất phẳng)**:
  * Nền cát sa mạc phẳng sử dụng prefab `GroundSand` để xe tăng di chuyển dễ dàng mà không bị lật.
* **Ground Props (Trang trí mặt đất)**:
  * `ConcreteSand` (9 khối bê tông bị chôn vùi dưới cát).
  * `Cow` (2 mô hình bộ xương bò khô héo đặc trưng cho sự khắc nghiệt của sa mạc, có gắn `CapsuleCollider` đóng vai trò vật cản nhỏ).
  * `OasisSandTrees` (ốc đảo nhỏ xanh mướt nổi bật giữa sa mạc).
* **Cacti (Xương rồng)**:
  * Các cụm xương rồng sa mạc chứa Collider, là chướng ngại vật cản đường đạn và xe tăng di chuyển.
* **Dunes (Cồn cát nhấp nhô)**:
  * 20 đụn cát nhấp nhô (`Dunes01` và `Dunes02`) được rải khắp bản đồ. Các đụn cát này có thành phần `NavMeshModifier` để cấu hình vùng di chuyển cho AI.
* **Military (Căn cứ quân sự)**:
  * Các lô cốt/nhà quân sự mái vòm cát (`BuildingSand01` và `BuildingSand02`).
  * Sân đáp trực thăng hình chữ H (`HelipadSand`).
  * Trạm radar viễn thông với chao cầu xoay (`RadarSphereSand`).
* **OilField (Khu mỏ dầu)**:
  * Nhà máy lọc dầu sa mạc (`RefinerySand`).
  * Bồn chứa dầu hình trụ (`OilStorage`).
  * Các máy gật gù bơm dầu (`PumpJackSand`) được trang bị thành phần `Animator` chạy clip điều khiển [CompletePumpjack.controller](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Art/Animators/CompletePumpjack.controller), tạo chuyển động gật gù liên tục sinh động khi game đang chạy.
* **Rocks (Đá sa mạc)**:
  * 23 cụm đá tảng khổng lồ (`RocksSand01` đến `RocksSand03`) được bố trí tạo hành lang di chuyển và điểm ẩn nấp lý tưởng khi đấu súng.
* **Ruins (Tàn tích)**:
  * `BustedTank` (xác một chiếc xe tăng cũ nát, gỉ sét bị hỏng).
  * `CraterSand01` (4 hố sụt bom lõm xuống đất).
  * `RuinsSand` (các khối tường đổ nát cổ xưa).
* **Trees (Cây cối)**:
  * 20 cây dừa/cọ sa mạc (`PalmTree`) tạo điểm nhấn xanh cho cảnh quan.

### 2.2. Điểm xuất phát (Spawn Points) & Điểm vật phẩm (Power-ups)
* **SpawnPoints**: Gồm 4 điểm xuất phát tĩnh (`SpawnPoint1` đến `SpawnPoint4`) phân bố ở bốn góc đấu trường để đảm bảo khoảng cách an toàn cho các xe tăng khi bắt đầu mỗi round đấu.
* **PowerupSpawners**: Gồm 4 vị trí spawn vật phẩm đặc biệt phân bố rải rác trên bản đồ. Mỗi điểm chứa script [PowerUpSpawner.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/PowerUp/PowerUpSpawner.cs). Khi bắt đầu game hoặc sau khi vật phẩm trước đó bị ăn 20 giây, spawner sẽ tự tạo ra một vật phẩm ngẫu nhiên.

---

## 3. Các cơ chế Logic & Script điều khiển chính

### 3.1. Quản lý vòng lặp Game - [GameManager.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/Managers/GameManager.cs)
* Quản lý luồng Coroutine điều hành trận đấu qua 3 giai đoạn chính:
  1. `RoundStarting`: Reset vị trí xe tăng, vô hiệu hóa điều khiển, định vị lại góc camera ban đầu và đếm ngược hiển thị số Round.
  2. `RoundPlaying`: Cho phép người chơi điều khiển và đợi cho đến khi chỉ còn $\le 1$ xe tăng sống sót trên đấu trường.
  3. `RoundEnding`: Vô hiệu hóa điều khiển, xác định xe thắng round, cộng điểm và kiểm tra xem có ai thắng đủ số vòng (ví dụ: 5 rounds) chưa để kết thúc trận đấu hoặc lặp lại vòng tiếp theo.

### 3.2. Điều khiển xe tăng - [TankMovement.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/Tank/TankMovement.cs)
* Nhận đầu vào (Input) từ người chơi và di chuyển xe tăng thông qua Rigidbody vật lý.
* Hỗ trợ 2 chế độ lái:
  * **Normal Tank Control**: Tiến/lùi bằng trục Vertical, xoay trái/phải bằng trục Horizontal.
  * **Direct/Camera-Relative Control** (Tự động kích hoạt khi dùng Gamepad hoặc bật Direct Control): Xe tăng sẽ tự động xoay đầu và tiến về hướng gạt cần analog/phím ấn tương ứng với góc nhìn camera của màn hình (ví dụ ấn Up xe tăng sẽ đi lên phía trên màn hình).
* Quản lý âm thanh động cơ xe tăng (thay đổi cao độ Pitch động giữa trạng thái đứng yên `EngineIdling` và di chuyển `EngineDriving`).
* Kích hoạt và dừng hệ thống hạt phun bụi đất (`DustTrails`) phía sau xích xe để tạo cảm giác di chuyển chân thực trên cát sa mạc.

### 3.3. Trí tuệ nhân tạo xe tăng máy - [TankAI.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/Tank/TankAI.cs)
Khi xe tăng được gán quyền điều khiển cho máy (Computer), script này sẽ kích hoạt và thực thi máy trạng thái hữu hạn (FSM) gồm hai trạng thái:

1. **Seek (Đuổi theo & Tấn công)**:
   * AI sử dụng dữ liệu NavMesh (`NavMesh-GroundDesert.asset`) để tìm đường đi ngắn nhất đến đối thủ gần nhất trong bản đồ. Để tối ưu hóa hiệu năng, AI chỉ tính toán tìm đường (Pathfinding) sau mỗi khoảng thời gian ngẫu nhiên từ $0.3$ đến $0.6$ giây thay vì chạy mỗi frame.
   * AI di chuyển theo các điểm góc (corners) của đường đi tìm được.
   * Khi khoảng cách tới đối thủ nhỏ hơn tầm bắn tối đa và không có vật cản chắn tầm nhìn (kiểm tra bằng `NavMesh.Raycast`), AI sẽ dừng xe lại và bắt đầu sạc đạn (`StartCharging`), sau đó căn lực bắn vừa đủ khoảng cách và thả đạn thẳng vào mục tiêu.
2. **Flee (Rút lui / Né tránh)**:
   * Nếu AI ở quá gần mục tiêu trong hơn 2 giây (để tránh kẹt khi chạy vòng quanh nhau), hoặc khi bắn một mục tiêu đang đứng yên quá 2 giây (đoán là mục tiêu cũng đang nhắm bắn mình), AI sẽ chuyển sang trạng thái Flee.
   * Khi Flee, AI chọn một hướng ngẫu nhiên lệch góc từ $90^\circ$ đến $180^\circ$ so với hướng mục tiêu, tính toán một điểm đích ngẫu nhiên từ 5 đến 20 đơn vị và tìm đường di chuyển tới đó để tái lập khoảng cách ngắm bắn an toàn.
   * Sau khi tới điểm đích, AI quay trở lại trạng thái Seek.

### 3.4. Hệ thống nâng cấp sức mạnh - [PowerUp.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/PowerUp/PowerUp.cs)
Xe tăng di chuyển qua các điểm spawn có thể nhặt các loại hộp bổ trợ đặc biệt (chỉ được kích hoạt khi xe tăng chưa có hiệu ứng nâng cấp nào khác đang hoạt động):
* **Healing (Hồi máu)**: Hồi phục ngay lập tức $20$ điểm HP cho xe tăng.
* **Speed (Tăng tốc)**: Tăng tốc chạy thêm $5$ đơn vị và duy trì trong thời gian cụ thể.
* **DamageReduction (Giáp)**: Giảm $50\%$ sát thương nhận vào trong 5 giây.
* **ShootingBonus (Bắn nhanh)**: Giảm $50\%$ thời gian hồi chiêu sạc đạn trong 5 giây.
* **Invincibility (Bất tử)**: Bảo vệ hoàn toàn xe tăng khỏi mọi sát thương trong 5 giây.
* **DamageMultiplier (Đạn siêu cấp)**: Nhân đôi ($2\times$) sát thương từ vụ nổ đạn tiếp theo.

### 3.5. Hệ thống Camera động - [CameraControl.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/Camera/CameraControl.cs)
* Camera trong cảnh sử dụng chế độ Orthographic (chiếu song song) nhìn từ trên xuống.
* **Tính trung bình vị trí**: Mỗi khung hình vật lý (`FixedUpdate`), camera tự động tính toán vị trí trung bình cộng tọa độ của tất cả các xe tăng còn sống và di chuyển mượt mà (`SmoothDamp`) về tâm điểm đó.
* **Tự động zoom**: Camera tự động co giãn kích thước phóng đại (`orthographicSize`) dựa trên khoảng cách local xa nhất giữa các xe tăng hiện tại để đảm bảo không có xe tăng nào bị lọt ra ngoài rìa màn hình.
* **Rung màn hình (Screen Shake)**: Cung cấp hàm `Shake(duration, magnitude)` để làm rung giật nhẹ camera khi có đạn nổ ở gần, tăng cảm giác kịch tính cho trận đấu.

### 3.6. Cơ chế đạn nổ - [ShellExplosion.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/Shell/ShellExplosion.cs)
* Đạn pháo bay theo đường vòng cung vật lý. Khi chạm vào bất kỳ Collider nào, đạn sẽ kích nổ.
* Vụ nổ sử dụng `Physics.OverlapSphere` để quét toàn bộ xe tăng nằm trong bán kính nổ (`m_ExplosionRadius` = 5m).
* Sát thương và lực đẩy vật lý được tính toán tuyến tính dựa trên khoảng cách từ tâm nổ đến xe tăng (sát thương lớn nhất là 100 HP khi nổ trực diện, giảm dần về 0 khi ở rìa bán kính).
* Khi nổ, đạn pháo kích hoạt hiệu ứng rung màn hình thông qua `CameraControl.Instance.Shake`.

---

## 4. Thiết lập UI & Giao diện Lobby - [GameUIHandler.cs](file:///Users/cps/Documents/Unity/Tank/Assets/_Tanks/Scripts/UI/GameUIHandler.cs)

* **Stack Camera trong URP**: Khi khởi chạy, script tự động chèn camera giao diện UI của nó vào ngăn xếp camera (`cameraStack`) của Camera chính để đảm bảo UI được dựng đè lên cảnh 3D một cách chuẩn xác trong Universal Render Pipeline.
* **Start Menu Lobby**: 
  * Cung cấp 4 slot xe tăng (`StartMenuSlot`) hiển thị mô hình xoay tròn của xe tăng tương ứng.
  * Cho phép người chơi nhấn nút "+" để tham gia hoặc chọn chế độ điều khiển: máy tính bắn (AI), người chơi 1, hoặc người chơi 2.
  * Chỉ khi có tối thiểu 2 xe tăng tham gia trận đấu, nút "Start" mới chuyển sang màu sáng tương tác để bắt đầu trò chơi.
* **Giao diện di động**: Tự động hiển thị cụm điều khiển ảo virtual joystick trên màn hình (`MobileUIControl.Instance`) khi bắt đầu vòng chơi nếu đang chạy trên môi trường di động hoặc bật chế độ test cảm ứng.
* **Menu tạm dừng**: Hỗ trợ nút Pause trên màn hình để tạm dừng trận đấu giữa chừng.

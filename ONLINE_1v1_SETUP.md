# Thiết lập 1v1 Online — phần còn lại phải làm trong Unity Editor

Phần **code** đã xong (UI sảnh chờ + tạo/vào phòng + bắt đầu trận). Nhưng để chạy end-to-end
cần vài bước trong Editor, vì các manager mạng hiện **chỉ nằm trong `TestNetworkScene`**, còn
`Start`/`MainMenu` và các map `Desert/Jungle/Moon` thì chưa có.

## 1. Tạo bootstrap mạng tồn tại xuyên scene (BẮT BUỘC)
Sảnh chờ trong `MainMenu` gọi `NetworkLobbyManager.Instance` — hiện là `null` vì manager chỉ có
trong `TestNetworkScene`.

- Mở scene **`Start`** (build index 0, là scene chạy đầu tiên).
- Tạo 1 GameObject rỗng `NetworkBootstrap`, thêm các component:
  - **NetworkManager** — cấu hình **Unity Transport**; trong **Network Prefabs** đăng ký
    prefab **Tank** và prefab **Shell** (mọi thứ được `Spawn()`).
  - **UGSManager**
  - **NetworkLobbyManager**
- `UGSManager` và `NetworkLobbyManager` đã `DontDestroyOnLoad` trong `Awake`. Bật cả
  "Don't Destroy" cho NetworkManager để nó sống qua các lần đổi scene.
- **Xoá** NetworkManager/UGSManager/NetworkLobbyManager khỏi `TestNetworkScene` để tránh
  trùng singleton (giữ TestNetworkScene chỉ để test riêng nếu muốn).

## 2. Làm các map scene sẵn sàng cho mạng (Desert / Jungle / Moon)
Mỗi map hiện có `GameManager` cũ (offline), **không có NetworkObject**.

- Trên GameObject **`GameManager`** của từng map: thêm component **NetworkObject**
  (GameManager giờ là `NetworkBehaviour`; NetworkObject đặt sẵn trong scene sẽ được Netcode
  tự spawn khi `NetworkManager.SceneManager.LoadScene` tải map).
- Đảm bảo GameManager của map được gán đầy đủ như trong `TestNetworkScene`:
  `Tank1Prefab`/`Tank2Prefab` (prefab tank có NetworkObject + NetworkTransform/NetworkRigidbody),
  `m_SpawnPoints`, `CameraControl`, và prefab Menu (chứa `MessageTextReference`).
- Cách nhanh nhất: copy nguyên cụm GameManager + spawn points + camera từ `TestNetworkScene`
  sang từng map (hoặc tạo map mới dựa trên bản `TestNetworkScene`).

## 3. Prefab mạng
- **Tank prefab**: có `NetworkObject`, và `NetworkTransform` (hoặc `NetworkRigidbody`) để đồng bộ.
- **Shell prefab**: có `NetworkObject` + `NetworkRigidbody`/`NetworkTransform` (client mới thấy đạn bay).
- Cả hai đăng ký trong **Network Prefabs** của NetworkManager.

## 4. Build Settings
Đã bật sẵn: `Start, MainMenu, Desert, Jungle, Moon, TestNetworkScene`. `SceneManager.LoadScene`
theo tên chỉ chạy khi scene có trong danh sách và được bật — đã OK.

## 5. Test 1v1
Cần 2 instance game. Dùng **Multiplayer Play Mode** (package `com.unity.multiplayer.center`),
hoặc chạy 1 bản build + 1 lần Play trong Editor:
1. Máy A: MainMenu → SOLO ARENA (1v1) → **TẠO PHÒNG MỚI** → copy mã.
2. Máy B: MainMenu → SOLO ARENA (1v1) → nhập mã → **VÀO PHÒNG**.
3. Máy A (chủ phòng): chọn map → **BẮT ĐẦU TRẬN ĐẤU** → cả hai vào map, GameManager tự spawn
   tank khi đủ 2 người.

## Lưu ý: 2 bug Netcode còn tồn (không liên quan lobby, nhưng sẽ lỗi khi bắn/đổi vòng)
- `ShellExplosion` gọi `Destroy` NetworkObject trên **mọi client** → ném lỗi. Chỉ server được despawn.
- `TankMovement.OnEnable` set `isKinematic = false` không kiểm tra owner → tank đối thủ chạy
  physics cục bộ sai sau mỗi vòng. Cần thêm guard `IsSpawned && !IsOwner`.

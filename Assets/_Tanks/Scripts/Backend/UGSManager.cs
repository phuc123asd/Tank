using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace Tanks.Backend
{
    /// <summary>
    /// Lớp quản lý vòng đời khởi tạo của Unity Gaming Services (UGS) trên Client.
    /// Đóng vai trò thiết lập nền tảng kết nối mạng trước khi chạy bất kỳ dịch vụ đám mây nào khác.
    /// </summary>
    public class UGSManager : MonoBehaviour
    {
        // Singleton Instance để truy cập từ mọi Scene trong game
        public static UGSManager Instance { get; private set; }

        // Các sự kiện (Events) để báo cáo trạng thái cho giao diện UI hoặc hệ thống quản lý game
        public event Action OnInitialized;
        public event Action<string> OnSignInFailed;
        public event Action<string> OnPlayerSignedIn;

        // Trạng thái kiểm tra kết nối và đăng nhập
        public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;
        public bool IsSignedIn => AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => AuthenticationService.Instance.PlayerId;

        private void Awake()
        {
            // Áp dụng mô hình Singleton để đảm bảo không bị trùng lặp đối tượng quản lý UGS
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Giữ đối tượng này tồn tại xuyên suốt việc chuyển đổi Scene
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private async void Start()
        {
            // Bắt đầu luồng bất đồng bộ (async/await) để kết nối máy chủ UGS ngay khi game chạy
            await InitializeServicesAsync();
        }

        /// <summary>
        /// Bước 1: Khởi tạo các dịch vụ cốt lõi của Unity
        /// </summary>
        private async Task InitializeServicesAsync()
        {
            try
            {
                Debug.Log("[UGSManager] Đang khởi tạo Unity Services...");
                
                // Hàm này sẽ giao tiếp với Cloud, tải về các cấu hình dự án (Project ID, Environment ID) 
                // và thiết lập nền tảng cho Auth, Lobby, Relay hoạt động.
                await UnityServices.InitializeAsync();
                
                Debug.Log("[UGSManager] Khởi tạo Unity Services thành công!");
                
                // Kích hoạt sự kiện báo cáo hệ thống đã sẵn sàng
                OnInitialized?.Invoke();

                // Sau khi UGS Core khởi tạo thành công, tiến hành Đăng nhập ẩn danh mặc định để định danh thiết bị
                await SignInAnonymouslyAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGSManager] Lỗi khởi tạo Unity Services: {e.Message}");
            }
        }

        /// <summary>
        /// Bước 2: Đăng nhập ẩn danh dựa trên mã định danh phần cứng (Device ID)
        /// </summary>
        private async Task SignInAnonymouslyAsync()
        {
            try
            {
                Debug.Log("[UGSManager] Đang đăng nhập ẩn danh...");
                
                // Hàm này sẽ:
                // 1. Kiểm tra xem trên máy này đã có token phiên cũ chưa.
                // 2. Nếu chưa có, gửi Device ID lên Unity Cloud để xin cấp 1 tài khoản ẩn danh mới.
                // 3. Unity Cloud trả về Access Token (JWT) và lưu trữ cục bộ (mã hóa) trên thiết bị.
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                
                Debug.Log($"[UGSManager] Đăng nhập thành công! Player ID: {AuthenticationService.Instance.PlayerId}");
                OnPlayerSignedIn?.Invoke(AuthenticationService.Instance.PlayerId);
            }
            catch (AuthenticationException authException)
            {
                Debug.LogError($"[UGSManager] Đăng nhập thất bại (Lỗi nghiệp vụ Auth): {authException.Message}");
                OnSignInFailed?.Invoke(authException.Message);
            }
            catch (Exception e)
            {
                Debug.LogError($"[UGSManager] Đăng nhập thất bại (Lỗi kết nối mạng): {e.Message}");
                OnSignInFailed?.Invoke(e.Message);
            }
        }
    }
}

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
        public event Action<string> OnInitializationFailed;   // Lỗi ở bước khởi tạo UGS (cấu hình / mạng / timeout)

        // Máy trạng thái kết nối để UI phản hồi trực quan
        public enum ConnectionState { Connecting, Connected, Failed }
        public ConnectionState State { get; private set; } = ConnectionState.Connecting;
        public string LastError { get; private set; }
        private bool m_IsConnecting;

        // Trạng thái kiểm tra kết nối và đăng nhập.
        // AuthenticationService.Instance NÉM ServicesInitializationException nếu UnityServices chưa
        // khởi tạo xong, nên mọi truy cập phải đi qua IsInitialized trước.
        public bool IsInitialized => UnityServices.State == ServicesInitializationState.Initialized;
        public bool IsSignedIn => IsInitialized && AuthenticationService.Instance.IsSignedIn;
        public string PlayerId => IsInitialized ? AuthenticationService.Instance.PlayerId : null;

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

        private void Start()
        {
            // Bắt đầu luồng kết nối máy chủ UGS ngay khi game chạy
            RetryInitialization();
        }

        public void SignOutAndReturnToLogin()
        {
            ClearAuthenticationSession();
            UnityEngine.SceneManagement.SceneManager.LoadScene("Start");
        }

        // Bỏ OnApplicationQuit() để giữ lại Session Token giữa các lần tắt mở game.
        // Đảm bảo dữ liệu Cloud Save của user ẩn danh không bị mất.

        private static void ClearAuthenticationSession()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return;

            try
            {
                var authService = AuthenticationService.Instance;
                if (authService.IsSignedIn)
                    authService.SignOut(true);
                else if (authService.SessionTokenExists)
                    authService.ClearSessionToken();

                Debug.Log("[UGSManager] Đã xoá hoàn toàn phiên đăng nhập trên thiết bị.");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UGSManager] Không thể xoá phiên đăng nhập: {exception.Message}");
            }
        }

        /// <summary>
        /// Chạy (hoặc chạy lại) toàn bộ tiến trình khởi tạo + đăng nhập.
        /// Dùng cho nút "Thử lại" trên UI khi kết nối thất bại.
        /// </summary>
        public void RetryInitialization()
        {
            if (m_IsConnecting) return;   // tránh chạy chồng nhiều lần
            _ = InitializeServicesAsync();
        }

        /// <summary>
        /// Bước 1: Khởi tạo các dịch vụ cốt lõi của Unity (có Timeout + phân loại lỗi)
        /// </summary>
        private async Task InitializeServicesAsync()
        {
            m_IsConnecting = true;
            State = ConnectionState.Connecting;
            LastError = null;

            try
            {
                // Nếu đã khởi tạo + đăng nhập từ trước (ví dụ quay lại menu) thì coi như xong ngay.
                if (IsInitialized && IsSignedIn)
                {
                    State = ConnectionState.Connected;
                    OnInitialized?.Invoke();
                    OnPlayerSignedIn?.Invoke(PlayerId);
                    return;
                }

                Debug.Log("[UGSManager] Đang khởi tạo Unity Services...");

                var options = new Unity.Services.Core.InitializationOptions();
                
                // Sử dụng Profile ngẫu nhiên cho mỗi lần chạy để đảm bảo có thể mở 2-3 cửa sổ Game trên cùng 1 máy tính 
                // mà không bị đụng độ bộ nhớ đăng nhập (tránh lỗi chỉ đăng nhập được 1 user).
                string uniqueProfile = "profile_" + System.Guid.NewGuid().ToString().Substring(0, 8);
                options.SetProfile(uniqueProfile);

                // Thiết lập thời gian chờ tối đa (Timeout) là 8 giây để tránh treo giao diện.
                var initTask = UnityServices.InitializeAsync(options);
                var delayTask = Task.Delay(8000);
                var completedTask = await Task.WhenAny(initTask, delayTask);
                if (completedTask == delayTask)
                {
                    throw new TimeoutException("Kết nối tới UGS quá thời gian chờ (Timeout). Vui lòng kiểm tra mạng hoặc tắt VPN.");
                }

                await initTask; // Đợi tác vụ hoàn thành để ném ra lỗi thật sự (nếu có)

                Debug.Log("[UGSManager] Khởi tạo UGS thành công.");
                OnInitialized?.Invoke();

                // Kiểm tra xem đã có phiên đăng nhập cũ chưa
                if (AuthenticationService.Instance.IsSignedIn)
                {
                    State = ConnectionState.Connected;
                    Debug.Log($"[UGSManager] Phát hiện phiên đăng nhập cũ hoạt động. Player ID: {AuthenticationService.Instance.PlayerId}");
                    OnPlayerSignedIn?.Invoke(AuthenticationService.Instance.PlayerId);
                }
                else
                {
                    // Chỉ tự động đăng nhập ẩn danh khi KHÔNG ở scene Start (để người dùng nhập Username/Password)
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Start")
                    {
                        await SignInAnonymouslyAsync();
                    }
                }
            }
            catch (ServicesInitializationException ex)
            {
                LastError = $"Lỗi cấu hình UGS: {ex.Message}. Hãy đảm bảo Project đã được liên kết trong Project Settings -> Services.";
                State = ConnectionState.Failed;
                Debug.LogError($"[UGSManager] {LastError}");
                OnInitializationFailed?.Invoke(LastError);
            }
            catch (Exception ex)
            {
                LastError = $"Lỗi kết nối mạng: {ex.Message}";
                State = ConnectionState.Failed;
                Debug.LogError($"[UGSManager] {LastError}");
                OnInitializationFailed?.Invoke(LastError);
            }
            finally
            {
                m_IsConnecting = false;
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

                State = ConnectionState.Connected;
                Debug.Log($"[UGSManager] Đăng nhập thành công! Player ID: {AuthenticationService.Instance.PlayerId}");
                OnPlayerSignedIn?.Invoke(AuthenticationService.Instance.PlayerId);
            }
            catch (AuthenticationException authException)
            {
                LastError = authException.Message;
                State = ConnectionState.Failed;
                Debug.LogError($"[UGSManager] Đăng nhập thất bại (Lỗi nghiệp vụ Auth): {authException.Message}");
                OnSignInFailed?.Invoke(authException.Message);
            }
            catch (Exception e)
            {
                LastError = e.Message;
                State = ConnectionState.Failed;
                Debug.LogError($"[UGSManager] Đăng nhập thất bại (Lỗi kết nối mạng): {e.Message}");
                OnSignInFailed?.Invoke(e.Message);
            }
        }
    }
}

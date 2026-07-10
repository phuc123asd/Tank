using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Multiplayer;
using Unity.Netcode;

namespace Tanks.Backend
{
    public class NetworkLobbyManager : MonoBehaviour
    {
        public static NetworkLobbyManager Instance { get; private set; }

        public event Action<ISession> OnSessionCreated;
        public event Action<ISession> OnSessionJoined;
        public event Action OnSessionLeft;
        public event Action<string> OnSessionError;

        /// <summary>Một người chơi khác rời phòng (chỉ chủ phòng nhận được).</summary>
        public event Action<ulong> OnPeerLeft;

        public ISession CurrentSession { get; private set; }
        public bool IsInSession => CurrentSession != null;

        private bool m_IsBusy;   // Chặn gọi chồng Create/Join khi một tác vụ đang chạy
        private bool m_NetcodeHooked;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            UnhookNetcode();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Tạo một phòng chờ mới (Host) tích hợp sẵn Unity Relay
        /// </summary>
        public async Task CreateLobbyAsync(string sessionName, int maxPlayers = 2)
        {
            if (!CanStartSessionOperation()) return;

            m_IsBusy = true;
            try
            {
                Debug.Log($"[NetworkLobbyManager] Đang tạo session: {sessionName}...");

                // Thiết lập SessionOptions cấu hình sử dụng Relay và giới hạn số người chơi
                var options = new SessionOptions
                {
                    MaxPlayers = maxPlayers,
                    Name = sessionName
                }.WithRelayNetwork();

                // Tạo session thông qua Multiplayer Services
                CurrentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

                Debug.Log($"[NetworkLobbyManager] Tạo phòng thành công! Session ID: {CurrentSession.Id}, Join Code: {CurrentSession.Code}");

                HookNetcode();
                OnSessionCreated?.Invoke(CurrentSession);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyManager] Lỗi khi tạo phòng: {e.Message}");
                OnSessionError?.Invoke(e.Message);
            }
            finally
            {
                m_IsBusy = false;
            }
        }

        /// <summary>
        /// Tham gia phòng chờ hiện có bằng mã Join Code
        /// </summary>
        public async Task JoinLobbyByCodeAsync(string joinCode)
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                OnSessionError?.Invoke("Mã Join Code không hợp lệ!");
                return;
            }

            if (!CanStartSessionOperation()) return;

            m_IsBusy = true;
            try
            {
                Debug.Log($"[NetworkLobbyManager] Đang tham gia phòng với Code: {joinCode}...");

                CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);

                Debug.Log($"[NetworkLobbyManager] Tham gia phòng thành công! Session ID: {CurrentSession.Id}");

                HookNetcode();
                OnSessionJoined?.Invoke(CurrentSession);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyManager] Lỗi khi tham gia phòng: {e.Message}");
                OnSessionError?.Invoke(e.Message);
            }
            finally
            {
                m_IsBusy = false;
            }
        }

        /// <summary>
        /// Điều kiện chung để bắt đầu một tác vụ tạo/vào phòng.
        /// </summary>
        private bool CanStartSessionOperation()
        {
            // Chặn re-entrancy: đang có tác vụ tạo/vào phòng dở dang
            if (m_IsBusy) return false;

            // Đã ở trong một phòng thì không tạo/vào đè (tránh bỏ rơi session cũ)
            if (IsInSession)
            {
                OnSessionError?.Invoke("Bạn đang ở trong một phòng khác. Hãy rời phòng trước.");
                return false;
            }

            // Kiểm tra đăng nhập an toàn (tránh NRE khi thiếu UGSManager)
            if (UGSManager.Instance == null || !UGSManager.Instance.IsSignedIn)
            {
                OnSessionError?.Invoke("Bạn cần đăng nhập UGS trước khi vào phòng!");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Rời phòng chờ hiện tại một cách chủ động.
        /// </summary>
        public async Task LeaveLobbyAsync()
        {
            if (CurrentSession == null) return;

            var session = CurrentSession;
            // Xoá tham chiếu TRƯỚC khi await: nếu Netcode shutdown kích hoạt HandleLocalDisconnect
            // giữa chừng, nó sẽ thấy CurrentSession == null và không phát OnSessionLeft lần hai.
            CurrentSession = null;
            UnhookNetcode();

            try
            {
                Debug.Log($"[NetworkLobbyManager] Đang rời phòng: {session.Id}...");
                await session.LeaveAsync();
            }
            catch (Exception e)
            {
                // Rời phòng thất bại phía UGS không nên giữ người chơi kẹt lại trong UI.
                Debug.LogWarning($"[NetworkLobbyManager] Lỗi khi rời phòng: {e.Message}");
            }

            ShutdownNetcode();
            Debug.Log("[NetworkLobbyManager] Đã rời phòng.");
            OnSessionLeft?.Invoke();
        }

        /// <summary>
        /// Dọn dẹp session ngay lập tức khi mất kết nối ngoài ý muốn (host thoát, rớt mạng,
        /// transport lỗi). Không await để có thể gọi an toàn từ callback của Netcode.
        /// </summary>
        public void ForceEndSession(string reason)
        {
            if (CurrentSession == null)
            {
                ShutdownNetcode();
                return;
            }

            var session = CurrentSession;
            CurrentSession = null;
            UnhookNetcode();

            if (!string.IsNullOrEmpty(reason))
                OnSessionError?.Invoke(reason);

            // Fire-and-forget: chỉ để UGS biết ta đã đi, không chặn luồng chính.
            _ = LeaveQuietlyAsync(session);

            ShutdownNetcode();
            OnSessionLeft?.Invoke();
        }

        private static async Task LeaveQuietlyAsync(ISession session)
        {
            try { await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning($"[NetworkLobbyManager] Không rời được session: {e.Message}"); }
        }

        private static void ShutdownNetcode()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("[NetworkLobbyManager] Đã dừng Netcode.");
            }
        }

        // =====================================================================
        //  PHÁT HIỆN MẤT KẾT NỐI
        // =====================================================================

        private void HookNetcode()
        {
            if (m_NetcodeHooked || NetworkManager.Singleton == null) return;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            NetworkManager.Singleton.OnTransportFailure += HandleTransportFailure;
            m_NetcodeHooked = true;
        }

        private void UnhookNetcode()
        {
            if (!m_NetcodeHooked || NetworkManager.Singleton == null)
            {
                m_NetcodeHooked = false;
                return;
            }
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            NetworkManager.Singleton.OnTransportFailure -= HandleTransportFailure;
            m_NetcodeHooked = false;
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            // Chính mình bị ngắt -> phiên chơi kết thúc, bất kể đang là chủ phòng hay khách.
            if (clientId == nm.LocalClientId)
            {
                string reason = string.IsNullOrEmpty(nm.DisconnectReason)
                    ? "Mất kết nối tới phòng."
                    : nm.DisconnectReason;
                Debug.Log($"[NetworkLobbyManager] Mất kết nối: {reason}");
                ForceEndSession(reason);
                return;
            }

            // Là khách: Netcode cũng báo khi một peer khác rời. Chỉ khi chính chủ phòng biến mất
            // thì phiên mới thực sự chấm dứt; các peer khác không liên quan tới ta.
            if (!nm.IsServer)
            {
                if (clientId == NetworkManager.ServerClientId)
                    ForceEndSession("Chủ phòng đã rời trận.");
                return;
            }

            // Là chủ phòng: một người chơi khác rời đi. Giữ phòng lại, chỉ báo cho UI/GameManager.
            Debug.Log($"[NetworkLobbyManager] Client {clientId} đã rời phòng.");
            OnPeerLeft?.Invoke(clientId);
        }

        private void HandleTransportFailure()
        {
            Debug.LogError("[NetworkLobbyManager] Transport lỗi — kết thúc phiên.");
            ForceEndSession("Lỗi đường truyền. Vui lòng thử lại.");
        }
    }
}

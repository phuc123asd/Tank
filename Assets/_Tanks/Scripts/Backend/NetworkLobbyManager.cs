using System;
using System.Collections.Generic;
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

        // Roster của trận sắp bắt đầu. Host ghi dữ liệu này ngay trước khi đổi scene;
        // object tồn tại qua DontDestroyOnLoad nên GameManager có thể dùng đúng đội đã chọn ở lobby.
        public bool IsTeamMatch { get; private set; }
        public IReadOnlyDictionary<ulong, int> MatchTeams => m_MatchTeams;
        private readonly Dictionary<ulong, int> m_MatchTeams = new Dictionary<ulong, int>();

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
            ClearMatchRoster();
            try
            {
                await PrepareNetcodeForSessionStartAsync();
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
            ClearMatchRoster();
            try
            {
                await PrepareNetcodeForSessionStartAsync();
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

        private static async Task PrepareNetcodeForSessionStartAsync()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            Debug.LogWarning("[NetworkLobbyManager] Netcode vẫn đang chạy trước khi tạo/vào phòng mới. Dọn trạng thái cũ trước.");
            nm.Shutdown();

            for (int i = 0; i < 60 && nm != null && nm.IsListening; i++)
                await Task.Yield();
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
            ClearMatchRoster();
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
                ClearMatchRoster();
                ShutdownNetcode();
                return;
            }

            var session = CurrentSession;
            CurrentSession = null;
            ClearMatchRoster();
            UnhookNetcode();

            if (!string.IsNullOrEmpty(reason))
                OnSessionError?.Invoke(reason);

            // Fire-and-forget: chỉ để UGS biết ta đã đi, không chặn luồng chính.
            _ = LeaveQuietlyAsync(session);

            ShutdownNetcode();
            OnSessionLeft?.Invoke();
        }

        /// <summary>
        /// Chốt roster do host đã duyệt ở lobby. Team 0 là Xanh, team 1 là Đỏ.
        /// Dữ liệu chỉ phục vụ trận kế tiếp và được xoá khi session kết thúc.
        /// </summary>
        public void ConfigureMatchRoster(bool isTeamMatch, IReadOnlyDictionary<ulong, int> teams)
        {
            m_MatchTeams.Clear();
            IsTeamMatch = isTeamMatch;

            if (!isTeamMatch || teams == null)
                return;

            foreach (var entry in teams)
                m_MatchTeams[entry.Key] = Mathf.Clamp(entry.Value, 0, 1);
        }

        /// <summary>Khoá session trước khi tải map để không có người mới chen vào roster giữa trận.</summary>
        public async Task<bool> LockCurrentSessionAsync()
        {
            return await SetCurrentSessionLockAsync(true);
        }

        public async Task<bool> UnlockCurrentSessionAsync()
        {
            return await SetCurrentSessionLockAsync(false);
        }

        private async Task<bool> SetCurrentSessionLockAsync(bool isLocked)
        {
            if (CurrentSession == null) return false;

            try
            {
                IHostSession hostSession = CurrentSession.AsHost();
                hostSession.IsLocked = isLocked;
                await hostSession.SavePropertiesAsync();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyManager] Không cập nhật được trạng thái khoá session: {e.Message}");
                OnSessionError?.Invoke("Không thể cập nhật trạng thái khoá phòng.");
                return false;
            }
        }

        public bool TryGetMatchTeam(ulong clientId, out int team)
        {
            return m_MatchTeams.TryGetValue(clientId, out team);
        }

        public void ClearMatchRoster()
        {
            IsTeamMatch = false;
            m_MatchTeams.Clear();
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

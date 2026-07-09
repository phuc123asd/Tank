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

        public ISession CurrentSession { get; private set; }
        public bool IsInSession => CurrentSession != null;

        private bool m_IsBusy;   // Chặn gọi chồng Create/Join khi một tác vụ đang chạy

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

        /// <summary>
        /// Tạo một phòng chờ mới (Host) tích hợp sẵn Unity Relay
        /// </summary>
        public async Task CreateLobbyAsync(string sessionName, int maxPlayers = 2)
        {
            // Chặn re-entrancy: đang có tác vụ tạo/vào phòng dở dang
            if (m_IsBusy) return;

            // Đã ở trong một phòng thì không tạo đè (tránh bỏ rơi host cũ)
            if (IsInSession)
            {
                OnSessionError?.Invoke("Bạn đang ở trong một phòng khác. Hãy rời phòng trước.");
                return;
            }

            // Kiểm tra đăng nhập an toàn (tránh NRE khi thiếu UGSManager)
            if (UGSManager.Instance == null || !UGSManager.Instance.IsSignedIn)
            {
                OnSessionError?.Invoke("Bạn cần đăng nhập UGS trước khi tạo phòng!");
                return;
            }

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
            if (!UGSManager.Instance.IsSignedIn)
            {
                OnSessionError?.Invoke("Bạn cần đăng nhập UGS trước khi tham gia phòng!");
                return;
            }

            if (string.IsNullOrEmpty(joinCode))
            {
                OnSessionError?.Invoke("Mã Join Code không hợp lệ!");
                return;
            }

            try
            {
                Debug.Log($"[NetworkLobbyManager] Đang tham gia phòng với Code: {joinCode}...");
                
                // Tham gia session
                CurrentSession = await MultiplayerService.Instance.JoinSessionByCodeAsync(joinCode);

                Debug.Log($"[NetworkLobbyManager] Tham gia phòng thành công! Session ID: {CurrentSession.Id}");
                
                OnSessionJoined?.Invoke(CurrentSession);
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyManager] Lỗi khi tham gia phòng: {e.Message}");
                OnSessionError?.Invoke(e.Message);
            }
        }

        /// <summary>
        /// Rời phòng chờ hiện tại
        /// </summary>
        public async Task LeaveLobbyAsync()
        {
            if (CurrentSession == null) return;

            try
            {
                Debug.Log($"[NetworkLobbyManager] Đang rời phòng: {CurrentSession.Id}...");
                await CurrentSession.LeaveAsync();
                
                // Tắt kết nối Netcode nếu đang chạy
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.Shutdown();
                    Debug.Log("[NetworkLobbyManager] Đã dừng Netcode.");
                }

                CurrentSession = null;
                Debug.Log("[NetworkLobbyManager] Đã rời phòng thành công.");
                OnSessionLeft?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NetworkLobbyManager] Lỗi khi rời phòng: {e.Message}");
                OnSessionError?.Invoke(e.Message);
            }
        }
    }
}

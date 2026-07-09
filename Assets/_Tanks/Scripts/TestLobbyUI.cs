using System;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Netcode;

namespace Tanks.Backend
{
    public class TestLobbyUI : MonoBehaviour
    {
        private string _joinCodeInput = "";
        private string _statusText = "Đang khởi tạo...";

        private void Start()
        {
            // Đăng ký các sự kiện từ UGSManager
            if (UGSManager.Instance != null)
            {
                UGSManager.Instance.OnInitialized += () => UpdateStatus("UGS đã khởi tạo thành công. Đang đăng nhập...");
                UGSManager.Instance.OnPlayerSignedIn += (playerId) => UpdateStatus($"Đăng nhập thành công! PlayerID: {playerId}");
                UGSManager.Instance.OnSignInFailed += (error) => UpdateStatus($"Đăng nhập thất bại: {error}");
            }

            // Đăng ký các sự kiện từ NetworkLobbyManager
            if (NetworkLobbyManager.Instance != null)
            {
                NetworkLobbyManager.Instance.OnSessionCreated += (session) => UpdateStatus($"Đã tạo phòng! Code: {session.Code}");
                NetworkLobbyManager.Instance.OnSessionJoined += (session) => UpdateStatus($"Đã vào phòng! Session ID: {session.Id}");
                NetworkLobbyManager.Instance.OnSessionLeft += () => UpdateStatus("Đã rời phòng thành công.");
                NetworkLobbyManager.Instance.OnSessionError += (error) => UpdateStatus($"Lỗi phòng: {error}");
            }
        }

        private void Update()
        {
            // Cập nhật trạng thái kết nối Netcode trực quan
            if (NetworkLobbyManager.Instance != null && NetworkLobbyManager.Instance.IsInSession)
            {
                var session = NetworkLobbyManager.Instance.CurrentSession;
                if (NetworkManager.Singleton != null)
                {
                    if (NetworkManager.Singleton.IsHost)
                    {
                        _statusText = $"[Chủ phòng] Code: {session.Code} | Số người: {NetworkManager.Singleton.ConnectedClientsList.Count}/2";
                    }
                    else if (NetworkManager.Singleton.IsClient)
                    {
                        _statusText = $"[Khách] Đã kết nối tới Host qua Relay.";
                    }
                }
            }
        }

        private void UpdateStatus(string message)
        {
            _statusText = message;
            Debug.Log($"[TestLobbyUI] {message}");
        }

        private void OnGUI()
        {
            // Thiết lập khu vực hiển thị GUI ở góc trái màn hình
            GUILayout.BeginArea(new Rect(20, 20, 400, 300));

            // Hiển thị dòng trạng thái
            GUILayout.Label($"<b>Trạng thái:</b> {_statusText}", new GUIStyle(GUI.skin.label) { richText = true });

            if (UGSManager.Instance == null || NetworkLobbyManager.Instance == null)
            {
                GUILayout.Label("Lỗi: Không tìm thấy UGSManager hoặc NetworkLobbyManager trong Scene!");
                GUILayout.EndArea();
                return;
            }

            // Nếu người chơi chưa đăng nhập thành công vào UGS, chờ đăng nhập
            if (!UGSManager.Instance.IsSignedIn)
            {
                GUILayout.Label("Đang chờ xác thực UGS...");
                GUILayout.EndArea();
                return;
            }

            // Giao diện khi CHƯA ở trong phòng chơi nào
            if (!NetworkLobbyManager.Instance.IsInSession)
            {
                GUILayout.Space(10);
                
                // Nút Tạo phòng (Làm Host)
                if (GUILayout.Button("Tạo Phòng Trực Tuyến (Làm Host)", GUILayout.Height(40)))
                {
                    UpdateStatus("Đang yêu cầu tạo phòng chơi...");
                    // Tạo phòng tên "1v1 Online", giới hạn tối đa 2 người chơi
                    _ = NetworkLobbyManager.Instance.CreateLobbyAsync("1v1 Online", 2);
                }

                GUILayout.Space(20);
                GUILayout.Label("Nhập mã phòng để tham gia:");
                
                // Ô nhập Join Code
                _joinCodeInput = GUILayout.TextField(_joinCodeInput, 10, GUILayout.Height(30));

                GUILayout.Space(5);

                // Nút Vào phòng (Làm Client)
                if (GUILayout.Button("Vào Phòng Bằng Mã Code (Làm Client)", GUILayout.Height(40)))
                {
                    if (string.IsNullOrEmpty(_joinCodeInput))
                    {
                        UpdateStatus("Vui lòng điền mã Join Code!");
                    }
                    else
                    {
                        UpdateStatus($"Đang kết nối tới mã phòng: {_joinCodeInput.ToUpper()}...");
                        _ = NetworkLobbyManager.Instance.JoinLobbyByCodeAsync(_joinCodeInput.ToUpper());
                    }
                }
            }
            // Giao diện khi ĐÃ Ở TRONG phòng chơi
            else
            {
                GUILayout.Space(20);
                
                // Nút Rời phòng
                if (GUILayout.Button("Rời Phòng Chơi", GUILayout.Height(40)))
                {
                    UpdateStatus("Đang rời phòng chơi...");
                    _ = NetworkLobbyManager.Instance.LeaveLobbyAsync();
                }
            }

            GUILayout.EndArea();
        }
    }
}

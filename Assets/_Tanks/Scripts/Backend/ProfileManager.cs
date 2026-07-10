using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.CloudSave;
using Unity.Services.Authentication;

namespace Tanks.Backend
{
    /// <summary>
    /// Quản lý hồ sơ người chơi (Profile) và lưu trữ dữ liệu lên Unity Cloud Save.
    /// </summary>
    public class ProfileManager : MonoBehaviour
    {
        public static ProfileManager Instance { get; private set; }

        public event Action<string> OnProfileLoaded;
        public event Action<string> OnProfileSaveFailed;
        public event Action<string> OnProfileSaveSuccess;

        public string DisplayName { get; private set; } = "Player";
        public string Rank { get; private set; } = "BẠCH KIM"; // Rank mặc định tạm thời
        public string AvatarId { get; private set; } = "avatar_1"; // Ảnh mặc định

        private const string KEY_DISPLAY_NAME = "display_name";
        private const string KEY_AVATAR_ID = "avatar_id";

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

        private void Start()
        {
            // Lắng nghe sự kiện đăng nhập thành công của UGSManager để tự động load profile
            if (UGSManager.Instance != null)
            {
                UGSManager.Instance.OnPlayerSignedIn += HandlePlayerSignedIn;
                if (UGSManager.Instance.IsSignedIn)
                {
                    _ = LoadProfileAsync();
                }
            }
        }

        private void OnDestroy()
        {
            if (UGSManager.Instance != null)
            {
                UGSManager.Instance.OnPlayerSignedIn -= HandlePlayerSignedIn;
            }
        }

        private void HandlePlayerSignedIn(string playerId)
        {
            _ = LoadProfileAsync();
        }

        /// <summary>
        /// Đọc thông tin Profile từ Cloud Save.
        /// </summary>
        public async Task LoadProfileAsync()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.LogWarning("[ProfileManager] Chưa đăng nhập, không thể load profile.");
                return;
            }

            try
            {
                Debug.Log("[ProfileManager] Đang tải dữ liệu hồ sơ từ Cloud Save...");
                var keys = new HashSet<string> { KEY_DISPLAY_NAME, KEY_AVATAR_ID };
                var results = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (results.TryGetValue(KEY_DISPLAY_NAME, out var item))
                {
                    DisplayName = item.Value.GetAs<string>();
                    Debug.Log($"[ProfileManager] Đã tải display_name thành công: '{DisplayName}'");
                }
                else
                {
                    // Nếu chưa có tên hiển thị trên cloud, mặc định là Username của tài khoản (hoặc ID ẩn danh)
                    string playerId = AuthenticationService.Instance.PlayerId;
                    string suffix = playerId.Length > 4 ? playerId.Substring(playerId.Length - 4) : playerId;
                    DisplayName = $"Player_{suffix}";
                    Debug.Log($"[ProfileManager] Không tìm thấy display_name cũ, đặt mặc định: '{DisplayName}'");
                }

                if (results.TryGetValue(KEY_AVATAR_ID, out var avatarItem))
                {
                    AvatarId = avatarItem.Value.GetAs<string>();
                    Debug.Log($"[ProfileManager] Đã tải avatar_id thành công: '{AvatarId}'");
                }
                else
                {
                    AvatarId = "avatar_1";
                    Debug.Log($"[ProfileManager] Không tìm thấy avatar_id cũ, đặt mặc định: '{AvatarId}'");
                }

                OnProfileLoaded?.Invoke(DisplayName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Tải dữ liệu hồ sơ thất bại: {ex.Message}");
                OnProfileLoaded?.Invoke(DisplayName); // Vẫn gọi để cập nhật UI với tên mặc định
            }
        }

        /// <summary>
        /// Lưu Hồ Sơ (Tên hiển thị và AvatarId) mới lên Cloud Save.
        /// </summary>
        public async Task SaveProfileAsync(string newName, string newAvatarId)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                OnProfileSaveFailed?.Invoke("Chưa đăng nhập, không thể lưu.");
                return;
            }

            if (string.IsNullOrEmpty(newName))
            {
                OnProfileSaveFailed?.Invoke("Tên hiển thị không được để trống.");
                return;
            }

            if (string.IsNullOrEmpty(newAvatarId))
            {
                newAvatarId = "avatar_1";
            }

            try
            {
                Debug.Log($"[ProfileManager] Đang lưu display_name mới: '{newName}' và avatar_id: '{newAvatarId}' lên Cloud Save...");
                var data = new Dictionary<string, object>
                {
                    { KEY_DISPLAY_NAME, newName },
                    { KEY_AVATAR_ID, newAvatarId }
                };

                await CloudSaveService.Instance.Data.Player.SaveAsync(data);
                DisplayName = newName;
                AvatarId = newAvatarId;
                Debug.Log($"[ProfileManager] Ghi vào cloud thành công! (Name: {newName}, Avatar: {newAvatarId})");
                OnProfileSaveSuccess?.Invoke(newName);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileManager] Lưu display_name thất bại: {ex.Message}");
                OnProfileSaveFailed?.Invoke(ex.Message);
            }
        }
    }
}

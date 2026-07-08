using System;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tanks.Complete
{
    /// <summary>
    /// Orchestrates going online: it ties together <see cref="UGSBootstrap"/> (sign-in),
    /// <see cref="RelayManager"/> (connectivity) and <see cref="LobbyManager"/> (rooms) and drives
    /// the NGO <see cref="NetworkManager"/> to start a host or client.
    ///
    /// Persistent singleton so the connection survives the load into the gameplay arena scene.
    /// UI (e.g. <see cref="OnlineMenuController"/>) reads <see cref="StatusMessage"/>/<see cref="LobbyCode"/>.
    /// </summary>
    public class OnlineGameConnector : MonoBehaviour
    {
        public static OnlineGameConnector Instance { get; private set; }

        [Tooltip("Total players allowed in a room, including the host.")]
        public int m_MaxPlayers = 4;

        [Tooltip("The networked gameplay scene the host loads (clients auto-sync to it via NGO).")]
        public string m_ArenaSceneName = "Online_Arena";

        public string StatusMessage { get; private set; } = string.Empty;
        public string LobbyCode { get; private set; } = string.Empty;
        public bool IsBusy { get; private set; }

        public static OnlineGameConnector Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("OnlineGameConnector");
                Instance = go.AddComponent<OnlineGameConnector>();
            }

            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Host a new room. On success <see cref="LobbyCode"/> holds the code to share.</summary>
        public async Task HostAsync()
        {
            if (!await EnsureReadyAsync())
                return;

            IsBusy = true;
            try
            {
                if (NetworkManager.Singleton == null)
                {
                    StatusMessage = "Loi: khong tim thay NetworkManager trong scene.";
                    return;
                }

                // Clean up any leftover session (e.g. a previous host/join attempt) before starting.
                await ResetIfNeededAsync();

                StatusMessage = "Dang tao Relay...";
                // Relay maxConnections excludes the host itself.
                string relayJoinCode = await RelayManager.CreateRelayAsync(m_MaxPlayers - 1);

                if (!NetworkManager.Singleton.StartHost())
                {
                    StatusMessage = "Loi: StartHost that bai.";
                    await CleanupAfterFailureAsync();
                    return;
                }

                // Move everyone into the gameplay arena over the network. Only the server initiates the
                // load; clients that join later are auto-synchronised to this scene by NGO.
                NetworkManager.Singleton.SceneManager.LoadScene(m_ArenaSceneName, LoadSceneMode.Single);

                StatusMessage = "Dang tao phong...";
                LobbyCode = await LobbyManager.Ensure().CreateLobbyAsync("Tank Room", m_MaxPlayers, relayJoinCode);
                StatusMessage = $"Dang host. Ma phong: {LobbyCode}";
            }
            catch (Exception e)
            {
                StatusMessage = $"Host that bai: {e.Message}";
                Debug.LogError($"[Online] Host failed: {e}");
                await CleanupAfterFailureAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Join an existing room by its lobby code.</summary>
        public async Task JoinAsync(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                StatusMessage = "Hay nhap ma phong.";
                return;
            }

            if (!await EnsureReadyAsync())
                return;

            IsBusy = true;
            try
            {
                if (NetworkManager.Singleton == null)
                {
                    StatusMessage = "Loi: khong tim thay NetworkManager trong scene.";
                    return;
                }

                // Clean up any leftover session before joining a new one.
                await ResetIfNeededAsync();

                StatusMessage = "Dang vao phong...";
                string relayJoinCode = await LobbyManager.Ensure().JoinLobbyByCodeAsync(lobbyCode.Trim());
                if (string.IsNullOrEmpty(relayJoinCode))
                {
                    StatusMessage = "Loi: phong khong co ma Relay.";
                    await CleanupAfterFailureAsync();
                    return;
                }

                StatusMessage = "Dang ket noi Relay...";
                await RelayManager.JoinRelayAsync(relayJoinCode);

                if (!NetworkManager.Singleton.StartClient())
                {
                    StatusMessage = "Loi: StartClient that bai.";
                    await CleanupAfterFailureAsync();
                    return;
                }

                LobbyCode = lobbyCode.Trim();
                StatusMessage = $"Da ket noi phong {LobbyCode}.";
            }
            catch (Exception e)
            {
                StatusMessage = $"Vao phong that bai: {e.Message}";
                Debug.LogError($"[Online] Join failed: {e}");
                await CleanupAfterFailureAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Shut down any active Netcode session and leave the current lobby, so a fresh Host/Join can start.
        private async Task ResetIfNeededAsync()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsListening || nm.IsServer || nm.IsClient))
                nm.Shutdown();

            if (LobbyManager.Instance != null && LobbyManager.Instance.CurrentLobby != null)
                await LobbyManager.Instance.LeaveLobbyAsync();
        }

        private async Task CleanupAfterFailureAsync()
        {
            try
            {
                await ResetIfNeededAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Online] Cleanup after failure hit: {e.Message}");
            }

            LobbyCode = string.Empty;
        }

        // Ensure UGS is signed in before any lobby/relay call.
        private async Task<bool> EnsureReadyAsync()
        {
            var ugs = UGSBootstrap.Ensure();
            if (!ugs.IsSignedIn)
            {
                StatusMessage = "Dang dang nhap...";
                await ugs.InitializeAndSignInAsync();
            }

            if (!ugs.IsSignedIn)
            {
                StatusMessage = "Chua dang nhap duoc UGS.";
                return false;
            }

            return true;
        }
    }
}

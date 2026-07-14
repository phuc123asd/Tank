using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Tanks.Backend;

namespace Tanks.Complete
{
    public class GameManager : NetworkBehaviour
    {
        // Which state the game is currently in
        public enum GameState
        {
            MainMenu,
            Game
        }

        // Data about the selected tanks passed from the menu to the GameManager
        public class PlayerData
        {
            public bool IsComputer;
            public Color TankColor;
            public GameObject UsedPrefab;
            public int ControlIndex;
        }
        
        public int m_NumRoundsToWin = 5;            // The number of rounds a single player has to win to win the game.
        public float m_StartDelay = 3f;             // The delay between the start of RoundStarting and RoundPlaying phases.
        public float m_EndDelay = 3f;               // The delay between the end of RoundPlaying and RoundEnding phases.
        public CameraControl m_CameraControl;       // Reference to the CameraControl script for control during different phases.

        [Header("Tanks Prefabs")]
        public GameObject m_Tank1Prefab;            // The Prefab used by the tank in Slot 1 of the Menu
        public GameObject m_Tank2Prefab;            // The Prefab used by the tank in Slot 2 of the Menu
        public GameObject m_Tank3Prefab;            // The Prefab used by the tank in Slot 3 of the Menu
        public GameObject m_Tank4Prefab;            // The Prefab used by the tank in Slot 4 of the Menu
        
        [FormerlySerializedAs("m_Tanks")] 
        public TankManager[] m_SpawnPoints;         // A collection of managers for enabling and disabling different aspects of the tanks.

        [Header("Spawn Layouts")]
        [SerializeField] private Transform[] m_DuelSpawnPoints = new Transform[2];
        [SerializeField] private Transform[] m_TeamSpawnPoints = new Transform[4];
        
        public NetworkVariable<bool> m_IsControlEnabled = new NetworkVariable<bool>(false);
        public NetworkVariable<FixedString512Bytes> m_TitleTextSync = new NetworkVariable<FixedString512Bytes>("");
        private bool m_GameLoopStarted = false;

        // [ONLINE] Trận bị bỏ dở (đối thủ thoát / mất kết nối). Cắt vòng lặp round và về menu.
        private bool m_MatchAborted = false;
        private bool m_ReturningToMenu = false;

        // [ONLINE] Lựa chọn tank của từng máy (clientId -> chỉ số prefab 0..3).
        // Mỗi máy chỉ chọn xe của riêng mình rồi gửi lên server qua SubmitTankChoiceRpc.
        private readonly Dictionary<ulong, int> m_ClientTankChoice = new Dictionary<ulong, int>();

        // Roster online do server chốt một lần khi vào map. Mọi hệ thống (spawn, màu, damage,
        // round và disconnect) đều đọc cùng mapping này, không suy lại từ danh sách client hiện tại.
        private readonly Dictionary<ulong, int> m_ClientTeams = new Dictionary<ulong, int>();
        private readonly Dictionary<ulong, int> m_ClientSlots = new Dictionary<ulong, int>();
        private bool m_OnlineTeamMatch;
        private int m_ForcedWinningTeam = -1;

        private const int k_TeamSize = 2;
        private const int k_TeamPlayerCount = 4;
        private static readonly Color k_BlueTeamColor = new Color(0.10f, 0.55f, 1.00f, 1f);
        private static readonly Color k_RedTeamColor = new Color(1.00f, 0.22f, 0.16f, 1f);

        public override void OnNetworkSpawn()
        {
            m_IsControlEnabled.OnValueChanged += OnControlEnabledChanged;
            m_TitleTextSync.OnValueChanged += OnTitleTextChanged;

            if (IsServer && NetworkManager.Singleton != null)
            {
                InitializeOnlineRoster();
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnectedFromMatch;
            }

            OnControlEnabledChanged(false, m_IsControlEnabled.Value);
            OnTitleTextChanged("", m_TitleTextSync.Value);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            m_IsControlEnabled.OnValueChanged -= OnControlEnabledChanged;
            m_TitleTextSync.OnValueChanged -= OnTitleTextChanged;

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnectedFromMatch;
        }

        // =====================================================================
        //  [ONLINE] Đối thủ thoát giữa chừng / kết thúc trận
        // =====================================================================

        // 2v2 đang chơi tiếp tục dưới dạng 2v1. Nếu thiếu người trước khi spawn thì dừng có kiểm
        // soát để không rơi vào trạng thái 3 người/free-for-all. 1v1 vẫn kết thúc khi một bên rời.
        private void OnClientDisconnectedFromMatch(ulong clientId)
        {
            if (!IsServer || m_MatchAborted || NetworkManager.Singleton == null) return;
            if (clientId == NetworkManager.Singleton.LocalClientId) return;   // chính server tắt

            m_ClientTankChoice.Remove(clientId);

            if (!m_GameLoopStarted)
            {
                AbortOnlineMatch("KHÔNG ĐỦ NGƯỜI - TRẬN ĐẤU ĐÃ HỦY");
                return;
            }

            if (!IsOnlineTeamMatch || !m_ClientTeams.TryGetValue(clientId, out int departedTeam))
            {
                AbortOnlineMatch("ĐỐI THỦ ĐÃ RỜI TRẬN");
                return;
            }

            if (m_ClientSlots.TryGetValue(clientId, out int slot) && slot >= 0 && slot < m_SpawnPoints.Length)
                m_SpawnPoints[slot].m_Instance = null;

            SetCameraTargets();
            int teammatesLeft = ConnectedPlayersInTeam(departedTeam, clientId);
            if (teammatesLeft > 0)
            {
                string message = departedTeam == 0
                    ? "ĐỘI XANH CÒN 1 NGƯỜI"
                    : "ĐỘI ĐỎ CÒN 1 NGƯỜI";
                Debug.Log($"[GameManager] Client {clientId} rời trận; tiếp tục 2v1.");
                ShowTemporaryTitle(message, 2f);
                return;
            }

            // Cả đội đã rời: kết thúc ngay trận hiện tại và trao chiến thắng cho đội còn lại.
            m_ForcedWinningTeam = 1 - departedTeam;
            TankManager winningCaptain = TeamCaptain(m_ForcedWinningTeam);
            if (winningCaptain != null)
                winningCaptain.m_Wins = Mathf.Max(winningCaptain.m_Wins, m_NumRoundsToWin - 1);
            SetTitleText(departedTeam == 0 ? "ĐỘI XANH ĐÃ RỜI TRẬN" : "ĐỘI ĐỎ ĐÃ RỜI TRẬN");
        }

        private void AbortOnlineMatch(string message)
        {
            m_MatchAborted = true;
            Debug.Log($"[GameManager] {message}");
            SetTitleText(message);
            ReturnEveryoneToMenu(m_EndDelay);
        }

        private int ConnectedPlayersInTeam(int team, ulong excludingClientId)
        {
            int count = 0;
            foreach (var entry in m_ClientTeams)
            {
                if (entry.Key == excludingClientId || entry.Value != team) continue;
                if (NetworkManager.Singleton.ConnectedClients.ContainsKey(entry.Key)) count++;
            }
            return count;
        }

        private void ShowTemporaryTitle(string message, float duration)
        {
            SetTitleText(message);
            StartCoroutine(ClearTemporaryTitle(message, duration));
        }

        private IEnumerator ClearTemporaryTitle(string message, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (!m_MatchAborted && m_TitleTextSync.Value.ToString() == message)
                SetTitleText("");
        }

        // Server ra lệnh cho mọi máy (kể cả chính nó) rời trận và quay về MainMenu.
        private void ReturnEveryoneToMenu(float delay)
        {
            if (!IsServer || m_ReturningToMenu) return;
            m_ReturningToMenu = true;

            ReturnToMenuClientRpc(delay);
            StartCoroutine(ReturnToMenuRoutine(delay));
        }

        [ClientRpc]
        private void ReturnToMenuClientRpc(float delay)
        {
            if (IsServer) return;   // server đã tự chạy coroutine của mình
            if (m_ReturningToMenu) return;
            m_ReturningToMenu = true;
            StartCoroutine(ReturnToMenuRoutine(delay));
        }

        // Cho người chơi kịp đọc thông báo, rồi cắt Netcode và tải MainMenu bằng SceneManager thường.
        // Không dùng NetworkManager.SceneManager ở đây: phiên chơi đang kết thúc, mỗi máy tự về menu.
        private IEnumerator ReturnToMenuRoutine(float delay)
        {
            m_MatchAborted = true;   // cắt vòng lặp `while (!OneTankLeft())` của RoundPlaying
            yield return new WaitForSeconds(delay);

            if (Tanks.Backend.NetworkLobbyManager.Instance != null)
                Tanks.Backend.NetworkLobbyManager.Instance.ForceEndSession(null);
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            SceneManager.LoadScene("MainMenu");
        }

        private void OnControlEnabledChanged(bool oldVal, bool newVal)
        {
            for (int i = 0; i < m_SpawnPoints.Length; i++)
            {
                if (m_SpawnPoints[i].m_Instance != null)
                {
                    if (newVal)
                        m_SpawnPoints[i].EnableControl();
                    else
                        m_SpawnPoints[i].DisableControl();
                }
            }
        }

        private void OnTitleTextChanged(FixedString512Bytes oldVal, FixedString512Bytes newVal)
        {
            if (m_TitleText != null)
            {
                m_TitleText.text = newVal.ToString();
            }
        }

        private void SetTitleText(string text)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                if (m_TitleText != null) m_TitleText.text = text;
                return;
            }

            if (IsServer)
            {
                m_TitleTextSync.Value = text;
            }
        }

        public void SyncPowerUpSelection(Vector3 spawnerPosition, int selectedIndex)
        {
            if (!IsServer)
                return;

            SyncPowerUpSelectionClientRpc(spawnerPosition, selectedIndex);
        }

        public void RequestPowerUpRespawn(Vector3 spawnerPosition)
        {
            if (IsServer)
                BeginPowerUpRespawn(spawnerPosition);
            else
                RequestPowerUpRespawnServerRpc(spawnerPosition);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPowerUpRespawnServerRpc(Vector3 spawnerPosition)
        {
            BeginPowerUpRespawn(spawnerPosition);
        }

        [ClientRpc]
        private void SyncPowerUpSelectionClientRpc(Vector3 spawnerPosition, int selectedIndex)
        {
            FindPowerUpSpawner(spawnerPosition)?.ApplySelectedPowerUp(selectedIndex);
        }

        private void BeginPowerUpRespawn(Vector3 spawnerPosition)
        {
            FindPowerUpSpawner(spawnerPosition)?.BeginRespawn();
        }

        private static PowerUpSpawner FindPowerUpSpawner(Vector3 position)
        {
            PowerUpSpawner closest = null;
            float closestDistance = float.MaxValue;

            foreach (var spawner in FindObjectsByType<PowerUpSpawner>(FindObjectsInactive.Exclude))
            {
                float distance = (spawner.transform.position - position).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = spawner;
                    closestDistance = distance;
                }
            }

            return closestDistance <= 0.01f ? closest : null;
        }

        // =====================================================================
        //  [ONLINE] Áp hiệu ứng power-up theo authority
        // =====================================================================
        // Gọi trên SERVER khi một tank nhặt power-up. Broadcast tới MỌI máy để mỗi máy áp hiệu ứng
        // lên bản tank cục bộ của mình. Nhờ đó component đúng authority tự có tác dụng:
        //  - owner: tốc độ (TankMovement), cooldown/đạn đặc biệt (TankShooting)
        //  - server: máu/khiên/bất tử (TankHealth)
        // và HUD hiện ở mọi máy.
        public void ApplyPowerUpToTank(NetworkObject tankObject, int powerUpType, float value1, float value2, float duration)
        {
            if (!IsServer || tankObject == null) return;
            ApplyPowerUpClientRpc(tankObject.NetworkObjectId, powerUpType, value1, value2, duration);
        }

        [ClientRpc]
        private void ApplyPowerUpClientRpc(ulong tankNetworkObjectId, int powerUpType, float value1, float value2, float duration)
        {
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(tankNetworkObjectId, out var netObj) || netObj == null)
                return;

            var detector = netObj.GetComponent<PowerUpDetector>();
            if (detector != null)
                detector.ApplyNetworkedPowerUp((PowerUp.PowerUpType)powerUpType, value1, value2, duration);
        }

        // =====================================================================
        //  [ONLINE] Hiệu ứng nổ đạn đồng bộ
        // =====================================================================
        // Gọi trên SERVER (host đã tự phát VFX cục bộ). Bảo các client còn lại phát VFX/âm thanh nổ
        // đúng vị trí server tính, trước khi đạn bị despawn -> hết cảnh nổ chập chờn / lệch chỗ.
        public void PlayShellExplosion(ulong shellNetworkObjectId, Vector3 position)
        {
            if (!IsServer) return;
            PlayShellExplosionClientRpc(shellNetworkObjectId, position);
        }

        [ClientRpc]
        private void PlayShellExplosionClientRpc(ulong shellNetworkObjectId, Vector3 position)
        {
            if (IsServer) return;   // host đã phát cục bộ trong ShellExplosion.Explode

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shellNetworkObjectId, out var netObj) && netObj != null)
            {
                var shell = netObj.GetComponent<ShellExplosion>();
                if (shell != null)
                    shell.PlayNetworkedExplosionEffect(position);
            }
        }

        private GameState m_CurrentState;
        
        private int m_RoundNumber;                  // Which round the game is currently on.
        private WaitForSeconds m_StartWait;         // Used to have a delay whilst the round starts.
        private WaitForSeconds m_EndWait;           // Used to have a delay whilst the round or game ends.
        private TankManager m_RoundWinner;          // Reference to the winner of the current round.  Used to make an announcement of who won.
        private TankManager m_GameWinner;           // Reference to the winner of the game.  Used to make an announcement of who won.

        private PlayerData[] m_TankData;            // Data passed from the menu about each selected tank (at least 2, max 4)
        private int m_PlayerCount = 0;              // Số slot của trận; online lấy từ roster server, offline lấy từ menu.
        private TextMeshProUGUI m_TitleText;        // The text used to display game message. Automatically found as part of the Menu prefab

        private void Start()
        {
            m_CurrentState = GameState.MainMenu;

            // Find the text used to display game info. Need to look at inactive object too, as the Menu prefab (which contains it) may be
            // disabled at the start when the user have a Title Screen which will enable the Menu.
            var textRef = FindAnyObjectByType<MessageTextReference>(FindObjectsInactive.Include);

            // If that text couldn't be found, we display an error and exit as it is required for the game manager to work
            if (textRef == null)
            {
                Debug.LogError("You need to add the Menus prefab in the scene to use the GameManager!");
                return;
            }

            m_TitleText = textRef.Text;
            SetTitleText("");
            // Offline games are started by GameUIHandler after the player picks exactly two tanks.
            // Starting here would spawn every scene spawn point before the menu selection exists.

            // The GameManager require 4 tanks prefabs, as the start menu have 4 fixed slot and need the 4 tanks to show there
            if (m_Tank1Prefab == null || m_Tank2Prefab == null || m_Tank3Prefab == null || m_Tank4Prefab == null)
            {
                Debug.LogError("You need to assign 4 tank prefab in the GameManager!");
            }
        }

        void GameStart()
        {
            if (!ApplySpawnLayout())
            {
                SetTitleText("CẤU HÌNH ĐIỂM XUẤT PHÁT KHÔNG HỢP LỆ");
                return;
            }

            m_StartWait = new WaitForSeconds (m_StartDelay);
            m_EndWait = new WaitForSeconds (m_EndDelay);

            SpawnAllTanks();
            SetCameraTargets();

            StartCoroutine (GameLoop ());
        }

        private bool ApplySpawnLayout()
        {
            Transform[] layout = m_OnlineTeamMatch ? m_TeamSpawnPoints : m_DuelSpawnPoints;
            int required = m_OnlineTeamMatch ? k_TeamPlayerCount : 2;

            if (m_SpawnPoints == null || m_SpawnPoints.Length < required ||
                layout == null || layout.Length < required)
            {
                Debug.LogError($"[GameManager] Thiếu spawn layout cho {(m_OnlineTeamMatch ? "2v2" : "đấu đôi")}.");
                return false;
            }

            for (int i = 0; i < required; i++)
            {
                if (layout[i] == null)
                {
                    Debug.LogError($"[GameManager] Spawn layout thiếu phần tử tại slot {i}.");
                    return false;
                }

                m_SpawnPoints[i].m_SpawnPoint = layout[i];
            }

            return true;
        }

        void ChangeGameState(GameState newState)
        {
            m_CurrentState = newState;

            switch (m_CurrentState)
            {
                case GameState.Game:
                    GameStart();
                    break;
            }
        }

        // Called by the menu, passing along the data from the selection made by the player in the menu
        public void StartGame(PlayerData[] playerData)
        {
            m_TankData = playerData;
            m_PlayerCount = m_TankData.Length;
            ChangeGameState(GameState.Game);
        }

        // =====================================================================
        //  [ONLINE] Mỗi máy tự chọn tank riêng
        // =====================================================================

        private void InitializeOnlineRoster()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            m_ClientTeams.Clear();
            m_ClientSlots.Clear();

            var connectedIds = new List<ulong>();
            foreach (var client in nm.ConnectedClientsList)
                connectedIds.Add(client.ClientId);
            connectedIds.Sort();

            NetworkLobbyManager lobby = NetworkLobbyManager.Instance;
            m_OnlineTeamMatch = (lobby != null && lobby.IsTeamMatch) || connectedIds.Count == k_TeamPlayerCount;

            if (m_OnlineTeamMatch)
            {
                var blue = new List<ulong>();
                var red = new List<ulong>();

                if (lobby != null && lobby.MatchTeams.Count == k_TeamPlayerCount)
                {
                    foreach (var entry in lobby.MatchTeams)
                    {
                        if (entry.Value == 0) blue.Add(entry.Key);
                        else red.Add(entry.Key);
                    }
                }

                // Fallback cho chạy test trực tiếp scene hoặc roster cũ/không hợp lệ.
                if (blue.Count != k_TeamSize || red.Count != k_TeamSize)
                {
                    blue.Clear();
                    red.Clear();
                    for (int i = 0; i < connectedIds.Count; i++)
                    {
                        if (i < k_TeamSize) blue.Add(connectedIds[i]);
                        else red.Add(connectedIds[i]);
                    }
                }

                blue.Sort();
                red.Sort();
                for (int i = 0; i < blue.Count && i < k_TeamSize; i++)
                    AssignRosterSlot(blue[i], 0, i);
                for (int i = 0; i < red.Count && i < k_TeamSize; i++)
                    AssignRosterSlot(red[i], 1, k_TeamSize + i);

                m_PlayerCount = k_TeamPlayerCount;
                Debug.Log($"[GameManager] Đã chốt roster 2v2: Xanh={blue.Count}, Đỏ={red.Count}.");
                return;
            }

            m_PlayerCount = Mathf.Min(connectedIds.Count, m_SpawnPoints.Length);
            for (int i = 0; i < m_PlayerCount; i++)
                m_ClientSlots[connectedIds[i]] = i;
        }

        private void AssignRosterSlot(ulong clientId, int team, int slot)
        {
            m_ClientTeams[clientId] = team;
            m_ClientSlots[clientId] = slot;
        }

        // Client gọi hàm này (RPC gửi tới server) để báo mình chọn xe nào.
        // rpcParams cho biết clientId của người gửi.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitTankChoiceRpc(int tankIndex, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!m_ClientSlots.ContainsKey(senderId))
            {
                Debug.LogWarning($"[GameManager] Bỏ qua lựa chọn tank từ client ngoài roster: {senderId}.");
                return;
            }
            m_ClientTankChoice[senderId] = Mathf.Clamp(tankIndex, 0, 3);
            int chosen = m_ClientTankChoice.Count;
            int needed = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsList.Count : 0;
            Debug.Log($"[GameManager] Client {senderId} chọn tank index {tankIndex}. Đã chọn {chosen}/{needed}.");
        }

        // Đủ người kết nối và tất cả đều đã gửi lựa chọn xe.
        private bool AllPlayersChosenTank()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return false;

            var clients = nm.ConnectedClientsList;
            int requiredPlayers = m_OnlineTeamMatch ? k_TeamPlayerCount : 2;
            if (clients.Count != requiredPlayers || m_ClientSlots.Count != requiredPlayers) return false;

            foreach (var clientId in m_ClientSlots.Keys)
            {
                if (!nm.ConnectedClients.ContainsKey(clientId) || !m_ClientTankChoice.ContainsKey(clientId))
                    return false;
            }
            return true;
        }

        // Ánh xạ chỉ số lựa chọn (0..3) sang prefab tank tương ứng của menu.
        private GameObject GetTankPrefabByIndex(int index)
        {
            switch (index)
            {
                case 1: return m_Tank2Prefab;
                case 2: return m_Tank3Prefab;
                case 3: return m_Tank4Prefab;
                default: return m_Tank1Prefab;
            }
        }

        // Màu cá nhân cho 1v1/offline. 2v2 không gọi nhánh này mà dùng GetTeamColor.
        private static readonly Color[] k_FallbackTankColors =
        {
            new Color(0.20f, 0.45f, 0.95f),   // xanh dương
            new Color(0.95f, 0.30f, 0.25f),   // đỏ
            new Color(0.30f, 0.80f, 0.35f),   // xanh lá
            new Color(0.95f, 0.80f, 0.20f),   // vàng
        };

        private Color GetTankColorByIndex(int index)
        {
            var ui = FindAnyObjectByType<GameUIHandler>(FindObjectsInactive.Include);
            if (ui != null && ui.m_PlayerSlots != null &&
                index >= 0 && index < ui.m_PlayerSlots.Length &&
                ui.m_PlayerSlots[index] != null)
            {
                return ui.m_PlayerSlots[index].m_SlotColor;
            }

            return k_FallbackTankColors[Mathf.Clamp(index, 0, k_FallbackTankColors.Length - 1)];
        }

        private static Color GetTeamColor(int team)
        {
            return team == 0 ? k_BlueTeamColor : k_RedTeamColor;
        }


        private void Update()
        {
            if (m_MatchAborted) return;

            if (IsServer)
            {
                if (m_OnlineTeamMatch && !m_GameLoopStarted &&
                    NetworkManager.Singleton.ConnectedClientsList.Count != k_TeamPlayerCount)
                {
                    AbortOnlineMatch("KHÔNG ĐỦ NGƯỜI - TRẬN ĐẤU ĐÃ HỦY");
                    return;
                }

                // Chỉ bắt đầu khi ĐỦ người VÀ mọi máy đã chọn xong xe của mình.
                if (!m_GameLoopStarted && AllPlayersChosenTank())
                {
                    m_GameLoopStarted = true;
                    Debug.Log("[GameManager] Tất cả người chơi đã chọn xe -> bắt đầu spawn.");
                    GameStart();
                }
            }
            else if (IsClient && m_PendingSlots.Count > 0)
            {
                ResolvePendingSlots();
            }
        }

        // =====================================================================
        //  [ONLINE] Server chỉ định slot cho từng xe, client không tự đoán
        // =====================================================================

        // Xe mà server đã báo nhưng NetworkObject chưa kịp spawn xong trên máy này.
        private readonly Dictionary<ulong, int> m_PendingSlots = new Dictionary<ulong, int>();

        // [ONLINE] Màu tank server gửi kèm slot để client tô đúng màu người chơi đã chọn ở menu.
        private readonly Dictionary<ulong, Color> m_PendingColors = new Dictionary<ulong, Color>();

        [ClientRpc]
        private void AssignTankSlotClientRpc(ulong networkObjectId, int slot, Color playerColor)
        {
            if (IsServer) return;   // server đã tự gán khi spawn

            m_PendingSlots[networkObjectId] = slot;
            m_PendingColors[networkObjectId] = playerColor;
            ResolvePendingSlots();
        }

        // Gắn các xe đã có NetworkObject vào đúng slot; xe nào chưa tới thì để lại cho frame sau.
        private void ResolvePendingSlots()
        {
            var spawned = NetworkManager.Singleton.SpawnManager.SpawnedObjects;

            m_ResolvedIds.Clear();
            foreach (var pending in m_PendingSlots)
            {
                if (!spawned.TryGetValue(pending.Key, out var netObj) || netObj == null)
                    continue;

                int slot = pending.Value;
                m_ResolvedIds.Add(pending.Key);

                if (slot < 0 || slot >= m_SpawnPoints.Length) continue;
                if (m_SpawnPoints[slot].m_Instance != null) continue;

                m_SpawnPoints[slot].m_Instance = netObj.gameObject;
                m_SpawnPoints[slot].m_PlayerNumber = slot + 1;
                m_SpawnPoints[slot].ControlIndex = -1;
                m_SpawnPoints[slot].m_ComputerControlled = false;
                // Tô đúng màu người chơi đã chọn trước khi Setup (Setup mới đọc m_PlayerColor để tint material).
                if (m_PendingColors.TryGetValue(pending.Key, out var pendingColor))
                    m_SpawnPoints[slot].m_PlayerColor = pendingColor;
                m_SpawnPoints[slot].Setup(this);

                // Make sure the newly discovered tank obeys the current control state
                if (m_IsControlEnabled.Value)
                    m_SpawnPoints[slot].EnableControl();
                else
                    m_SpawnPoints[slot].DisableControl();

                SetCameraTargets();
            }

            foreach (var id in m_ResolvedIds)
            {
                m_PendingSlots.Remove(id);
                m_PendingColors.Remove(id);
            }
        }

        private readonly List<ulong> m_ResolvedIds = new List<ulong>();

        private void SpawnAllTanks()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                m_PlayerCount = m_TankData != null && m_TankData.Length > 0
                    ? Mathf.Min(m_TankData.Length, m_SpawnPoints.Length)
                    : Mathf.Min(2, m_SpawnPoints.Length);

                for (int i = 0; i < m_PlayerCount; i++)
                {
                    GameObject tankPrefab = m_TankData != null && i < m_TankData.Length && m_TankData[i].UsedPrefab != null
                        ? m_TankData[i].UsedPrefab
                        : ((i == 0) ? m_Tank1Prefab : m_Tank2Prefab);
                    GameObject tankInstance = Instantiate(tankPrefab, m_SpawnPoints[i].m_SpawnPoint.position, m_SpawnPoints[i].m_SpawnPoint.rotation);

                    m_SpawnPoints[i].m_Instance = tankInstance;
                    m_SpawnPoints[i].m_PlayerNumber = i + 1;
                    m_SpawnPoints[i].ControlIndex = m_TankData != null && i < m_TankData.Length ? m_TankData[i].ControlIndex : (i == 0 ? 1 : -1);
                    m_SpawnPoints[i].m_ComputerControlled = m_TankData != null && i < m_TankData.Length ? m_TankData[i].IsComputer : (i > 0);
                    m_SpawnPoints[i].m_PlayerColor = m_TankData != null && i < m_TankData.Length ? m_TankData[i].TankColor : m_SpawnPoints[i].m_PlayerColor;
                }

                for (int i = 0; i < m_PlayerCount; i++)
                {
                    var tank = m_SpawnPoints[i];
                    if (tank.m_Instance == null) continue;
                    tank.Setup(this);
                }
                return;
            }

            if (!IsServer) return;

            var roster = new List<KeyValuePair<ulong, int>>(m_ClientSlots);
            roster.Sort((a, b) => a.Value.CompareTo(b.Value));
            if (!m_OnlineTeamMatch)
                m_PlayerCount = Mathf.Min(roster.Count, m_SpawnPoints.Length);

            foreach (var rosterEntry in roster)
            {
                ulong clientId = rosterEntry.Key;
                int slot = rosterEntry.Value;
                if (slot < 0 || slot >= m_SpawnPoints.Length ||
                    !NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
                    continue;

                // Dùng xe mà chính máy đó đã chọn; nếu thiếu (fallback) thì theo thứ tự slot.
                int choice = m_ClientTankChoice.TryGetValue(clientId, out var picked) ? picked : slot;
                GameObject tankPrefab = GetTankPrefabByIndex(choice);
                int team = m_ClientTeams.TryGetValue(clientId, out int assignedTeam) ? assignedTeam : -1;
                Color tankColor = m_OnlineTeamMatch ? GetTeamColor(team) : GetTankColorByIndex(choice);
                Debug.Log($"[GameManager] Spawn tank client {clientId}, slot {slot}, team {team}, prefab '{tankPrefab.name}'.");

                GameObject tankInstance = Instantiate(tankPrefab, m_SpawnPoints[slot].m_SpawnPoint.position, m_SpawnPoints[slot].m_SpawnPoint.rotation);
                var netObj = tankInstance.GetComponent<NetworkObject>();
                netObj.SpawnWithOwnership(clientId);

                m_SpawnPoints[slot].m_Instance = tankInstance;
                m_SpawnPoints[slot].m_PlayerNumber = slot + 1;
                m_SpawnPoints[slot].ControlIndex = -1;
                m_SpawnPoints[slot].m_ComputerControlled = false;
                m_SpawnPoints[slot].m_PlayerColor = tankColor;

                // Clients cannot infer the slot from OwnerClientId — that only matches while the
                // host happens to be first in ConnectedClientsList. Send the authoritative index.
                AssignTankSlotClientRpc(netObj.NetworkObjectId, slot, tankColor);
            }

            foreach (var tank in m_SpawnPoints)
            {
                if (tank.m_Instance == null) continue;
                tank.Setup(this);
            }
        }


        private void SetCameraTargets()
        {
            int count = 0;
            for (int i = 0; i < m_SpawnPoints.Length; i++)
            {
                if (m_SpawnPoints[i].m_Instance != null) count++;
            }

            Transform[] targets = new Transform[count];
            int index = 0;
            for (int i = 0; i < m_SpawnPoints.Length; i++)
            {
                if (m_SpawnPoints[i].m_Instance != null)
                {
                    targets[index++] = m_SpawnPoints[i].m_Instance.transform;
                }
            }

            m_CameraControl.m_Targets = targets;
        }


        // This is called from start and will run each phase of the game one after another.
        private IEnumerator GameLoop ()
        {
            // Start off by running the 'RoundStarting' coroutine but don't return until it's finished.
            yield return StartCoroutine (RoundStarting ());

            // Once the 'RoundStarting' coroutine is finished, run the 'RoundPlaying' coroutine but don't return until it's finished.
            yield return StartCoroutine (RoundPlaying());

            // The opponent left mid-round: ReturnToMenuRoutine is already taking us out, so stop here
            // rather than crowning a winner over a destroyed tank.
            if (m_MatchAborted)
                yield break;

            // Once execution has returned here, run the 'RoundEnding' coroutine, again don't return until it's finished.
            yield return StartCoroutine (RoundEnding());

            // This code is not run until 'RoundEnding' has finished.  At which point, check if a game winner has been found.
            if (m_GameWinner != null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    // Online: the guest cannot follow a plain SceneManager.LoadScene, so tell every
                    // machine to tear down its session and walk back to the menu on its own.
                    // RoundEnding already held the win message on screen, so leave right away.
                    ReturnEveryoneToMenu(0f);
                }
                else
                {
                    // If there is a game winner, return to the main menu so the player can pick again.
                    // Loaded by name (not index 0) because index 0 is now the Start screen.
                    SceneManager.LoadScene ("MainMenu");
                }
            }
            else
            {
                // If there isn't a winner yet, restart this coroutine so the loop continues.
                // Note that this coroutine doesn't yield.  This means that the current version of the GameLoop will end.
                StartCoroutine (GameLoop ());
            }
        }


        private IEnumerator RoundStarting ()
        {
            // As soon as the round starts reset the tanks and make sure they can't move.
            ResetAllTanks ();
            DisableTankControl ();

            // Snap the camera's zoom and position to something appropriate for the reset tanks.
            m_CameraControl.SetStartPositionAndSize ();

            // Increment the round number and display text showing the players what round it is.
            m_RoundNumber++;
            SetTitleText("VÒNG " + m_RoundNumber);

            // Wait for the specified length of time until yielding control back to the game loop.
            yield return m_StartWait;
        }


        private IEnumerator RoundPlaying ()
        {
            // As soon as the round begins playing let the players control the tanks.
            EnableTankControl ();

            // Clear the text from the screen.
            SetTitleText("");

            // While there is not one tank left...
            while (!OneTankLeft() && !m_MatchAborted)
            {
                // ... return on the next frame.
                yield return null;
            }
        }


        private IEnumerator RoundEnding ()
        {
            // Stop tanks from moving.
            DisableTankControl ();

            // Clear the winner from the previous round.
            m_RoundWinner = null;

            // See if there is a winner now the round is over.
            m_RoundWinner = GetRoundWinner ();

            // If there is a winner, increment their score.
            if (m_RoundWinner != null)
                m_RoundWinner.m_Wins++;

            // Now the winner's score has been incremented, see if someone has one the game.
            m_GameWinner = GetGameWinner ();

            // Get a message based on the scores and whether or not there is a game winner and display it.
            string message = EndMessage ();
            SetTitleText(message);

            // Wait for the specified length of time until yielding control back to the game loop.
            yield return m_EndWait;
        }


        // A tank counts as alive only if its instance still exists. When a player disconnects, Netcode
        // destroys the NetworkObject it owned, so m_Instance becomes a destroyed reference.
        private bool IsTankAlive(int index)
        {
            var instance = m_SpawnPoints[index].m_Instance;
            return instance != null && instance.activeSelf;
        }


        public bool IsOnlineTeamMatch => m_OnlineTeamMatch && NetworkManager.Singleton != null
            && NetworkManager.Singleton.IsListening;

        public bool ShouldIgnoreFriendlyFire(ulong sourceClientId, ulong targetClientId)
        {
            if (!IsOnlineTeamMatch || sourceClientId == targetClientId) return false;
            return m_ClientTeams.TryGetValue(sourceClientId, out int sourceTeam) &&
                   m_ClientTeams.TryGetValue(targetClientId, out int targetTeam) &&
                   sourceTeam == targetTeam;
        }

        private TankManager TeamCaptain(int team)
        {
            int slot = team == 0 ? 0 : k_TeamSize;
            return slot >= 0 && slot < m_SpawnPoints.Length ? m_SpawnPoints[slot] : null;
        }

        private int AliveInTeam(int team)
        {
            int start = team == 0 ? 0 : 2;
            int end = Mathf.Min(start + 2, m_PlayerCount);
            int alive = 0;
            for (int i = start; i < end; i++)
                if (IsTankAlive(i)) alive++;
            return alive;
        }

        // 1v1 kết thúc khi còn <= 1 tank; 2v2 kết thúc khi một đội bị loại hoàn toàn.
        private bool OneTankLeft()
        {
            if (IsOnlineTeamMatch)
                return AliveInTeam(0) == 0 || AliveInTeam(1) == 0;

            // Start the count of tanks left at zero.
            int numTanksLeft = 0;

            // Go through all the tanks...
            for (int i = 0; i < m_PlayerCount; i++)
            {
                // ... and if they are active, increment the counter.
                if (IsTankAlive(i))
                    numTanksLeft++;
            }

            // If there are one or fewer tanks remaining return true, otherwise return false.
            return numTanksLeft <= 1;
        }


        // This function is to find out if there is a winner of the round.
        // This function is called with the assumption that 1 or fewer tanks are currently active.
        private TankManager GetRoundWinner()
        {
            if (IsOnlineTeamMatch)
            {
                if (m_ForcedWinningTeam >= 0) return TeamCaptain(m_ForcedWinningTeam);
                if (AliveInTeam(0) > 0 && AliveInTeam(1) == 0) return m_SpawnPoints[0];
                if (AliveInTeam(1) > 0 && AliveInTeam(0) == 0) return m_SpawnPoints[2];
                return null;
            }

            // Go through all the tanks...
            for (int i = 0; i < m_PlayerCount; i++)
            {
                // ... and if one of them is active, it is the winner so return it.
                if (IsTankAlive(i))
                    return m_SpawnPoints[i];
            }

            // If none of the tanks are active it is a draw so return null.
            return null;
        }


        // This function is to find out if there is a winner of the game.
        private TankManager GetGameWinner()
        {
            if (IsOnlineTeamMatch)
            {
                if (m_SpawnPoints[0].m_Wins >= m_NumRoundsToWin) return m_SpawnPoints[0];
                if (m_SpawnPoints[2].m_Wins >= m_NumRoundsToWin) return m_SpawnPoints[2];
                return null;
            }

            // Go through all the tanks...
            for (int i = 0; i < m_PlayerCount; i++)
            {
                // ... and if one of them has enough rounds to win the game, return it.
                if (m_SpawnPoints[i].m_Wins == m_NumRoundsToWin)
                    return m_SpawnPoints[i];
            }

            // If no tanks have enough rounds to win, return null.
            return null;
        }


        // Returns a string message to display at the end of each round.
        private string EndMessage()
        {
            if (IsOnlineTeamMatch)
            {
                int blueWins = m_SpawnPoints[0].m_Wins;
                int redWins = m_SpawnPoints[2].m_Wins;
                if (m_GameWinner != null)
                    return m_GameWinner == m_SpawnPoints[0] ? "ĐỘI XANH THẮNG TRẬN!" : "ĐỘI ĐỎ THẮNG TRẬN!";

                string result = m_RoundWinner == null ? "HÒA!" :
                    (m_RoundWinner == m_SpawnPoints[0] ? "ĐỘI XANH THẮNG VÒNG!" : "ĐỘI ĐỎ THẮNG VÒNG!");
                return $"{result}\n\nĐỘI XANH: {blueWins}  -  ĐỘI ĐỎ: {redWins}";
            }

            // By default when a round ends there are no winners so the default end message is a draw.
            string message = "HÒA!";

            // If there is a winner then change the message to reflect that.
            if (m_RoundWinner != null)
                message = m_RoundWinner.m_ColoredPlayerText + " THẮNG VÒNG!";

            // Add some line breaks after the initial message.
            message += "\n\n\n\n";

            // Go through all the tanks and add each of their scores to the message.
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i].m_Instance == null) continue;
                message += m_SpawnPoints[i].m_ColoredPlayerText + ": " + m_SpawnPoints[i].m_Wins + " VÒNG THẮNG\n";
            }

            // If there is a game winner, change the entire message to reflect that.
            if (m_GameWinner != null)
                message = m_GameWinner.m_ColoredPlayerText + " THẮNG TRẬN!";

            return message;
        }


        // This function is used to turn all the tanks back on and reset their positions and properties.
        private void ResetAllTanks()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
                if (m_SpawnPoints[i].m_Instance == null) continue;

                m_SpawnPoints[i].Reset();

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    var netObj = m_SpawnPoints[i].m_Instance.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        ResetTankClientRpc(netObj.NetworkObjectId, m_SpawnPoints[i].m_SpawnPoint.position, m_SpawnPoints[i].m_SpawnPoint.rotation);
                    }
                }
            }
        }

        [ClientRpc]
        private void ResetTankClientRpc(ulong networkObjectId, Vector3 position, Quaternion rotation)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            {
                if (netObj.IsOwner)
                {
                    netObj.transform.position = position;
                    netObj.transform.rotation = rotation;
                    
                    var rb = netObj.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
                
                // SetActive does not automatically sync across the network for NetworkObjects
                // so we must explicitly enable it on all clients when resetting.
                netObj.gameObject.SetActive(false);
                netObj.gameObject.SetActive(true);
            }
        }


        private void EnableTankControl()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                for (int i = 0; i < m_SpawnPoints.Length; i++)
                {
                    if (m_SpawnPoints[i].m_Instance != null)
                        m_SpawnPoints[i].EnableControl();
                }
                return;
            }

            if (IsServer)
            {
                m_IsControlEnabled.Value = true;
            }
        }

        private void DisableTankControl()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                for (int i = 0; i < m_SpawnPoints.Length; i++)
                {
                    if (m_SpawnPoints[i].m_Instance != null)
                        m_SpawnPoints[i].DisableControl();
                }
                return;
            }

            if (IsServer)
            {
                m_IsControlEnabled.Value = false;
            }
        }
    }
}

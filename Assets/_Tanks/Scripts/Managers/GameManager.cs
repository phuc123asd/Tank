using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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
        
        public NetworkVariable<bool> m_IsControlEnabled = new NetworkVariable<bool>(false);
        public NetworkVariable<FixedString128Bytes> m_TitleTextSync = new NetworkVariable<FixedString128Bytes>("");
        private bool m_GameLoopStarted = false;

        // [ONLINE] Lựa chọn tank của từng máy (clientId -> chỉ số prefab 0..3).
        // Mỗi máy chỉ chọn xe của riêng mình rồi gửi lên server qua SubmitTankChoiceRpc.
        private readonly Dictionary<ulong, int> m_ClientTankChoice = new Dictionary<ulong, int>();

        public override void OnNetworkSpawn()
        {
            m_IsControlEnabled.OnValueChanged += OnControlEnabledChanged;
            m_TitleTextSync.OnValueChanged += OnTitleTextChanged;
            
            OnControlEnabledChanged(false, m_IsControlEnabled.Value);
            OnTitleTextChanged("", m_TitleTextSync.Value);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            m_IsControlEnabled.OnValueChanged -= OnControlEnabledChanged;
            m_TitleTextSync.OnValueChanged -= OnTitleTextChanged;
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

        private void OnTitleTextChanged(FixedString128Bytes oldVal, FixedString128Bytes newVal)
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
        private GameState m_CurrentState;
        
        private int m_RoundNumber;                  // Which round the game is currently on.
        private WaitForSeconds m_StartWait;         // Used to have a delay whilst the round starts.
        private WaitForSeconds m_EndWait;           // Used to have a delay whilst the round or game ends.
        private TankManager m_RoundWinner;          // Reference to the winner of the current round.  Used to make an announcement of who won.
        private TankManager m_GameWinner;           // Reference to the winner of the game.  Used to make an announcement of who won.

        private PlayerData[] m_TankData;            // Data passed from the menu about each selected tank (at least 2, max 4)
        private int m_PlayerCount = 0;              // The number of players (2 to 4), decided from the number of PlayerData passed by the menu
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
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                GameStart();
            }

            // The GameManager require 4 tanks prefabs, as the start menu have 4 fixed slot and need the 4 tanks to show there
            if (m_Tank1Prefab == null || m_Tank2Prefab == null || m_Tank3Prefab == null || m_Tank4Prefab == null)
            {
                Debug.LogError("You need to assign 4 tank prefab in the GameManager!");
            }
        }

        void GameStart()
        {
            m_StartWait = new WaitForSeconds (m_StartDelay);
            m_EndWait = new WaitForSeconds (m_EndDelay);

            SpawnAllTanks();
            SetCameraTargets();

            StartCoroutine (GameLoop ());
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

        // Client gọi hàm này (RPC gửi tới server) để báo mình chọn xe nào.
        // rpcParams cho biết clientId của người gửi.
        [Rpc(SendTo.Server)]
        public void SubmitTankChoiceRpc(int tankIndex, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
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
            if (clients.Count < 2) return false;   // 1v1 cần đủ 2 người

            for (int i = 0; i < clients.Count; i++)
            {
                if (!m_ClientTankChoice.ContainsKey(clients[i].ClientId))
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


        private void Update()
        {
            if (IsServer)
            {
                // Chỉ bắt đầu khi ĐỦ người VÀ mọi máy đã chọn xong xe của mình.
                if (!m_GameLoopStarted && AllPlayersChosenTank())
                {
                    m_GameLoopStarted = true;
                    Debug.Log("[GameManager] Tất cả người chơi đã chọn xe -> bắt đầu spawn.");
                    GameStart();
                }
            }
            else if (IsClient)
            {
                var networkObjects = FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude);
                foreach (var netObj in networkObjects)
                {
                    if (netObj.gameObject.name.Contains("Tank"))
                    {
                        ulong ownerId = netObj.OwnerClientId;
                        int slot = (ownerId == 0) ? 0 : 1;
                        if (slot < m_SpawnPoints.Length && m_SpawnPoints[slot].m_Instance == null)
                        {
                            m_SpawnPoints[slot].m_Instance = netObj.gameObject;
                            m_SpawnPoints[slot].m_PlayerNumber = slot + 1;
                            m_SpawnPoints[slot].Setup(this);
                            
                            // Make sure the newly discovered tank obeys the current control state
                            if (m_IsControlEnabled.Value)
                                m_SpawnPoints[slot].EnableControl();
                            else
                                m_SpawnPoints[slot].DisableControl();

                            SetCameraTargets();
                        }
                    }
                }
            }
        }

        private void SpawnAllTanks()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                m_PlayerCount = m_SpawnPoints.Length;
                for (int i = 0; i < m_PlayerCount; i++)
                {
                    GameObject tankPrefab = (i == 0) ? m_Tank1Prefab : m_Tank2Prefab;
                    GameObject tankInstance = Instantiate(tankPrefab, m_SpawnPoints[i].m_SpawnPoint.position, m_SpawnPoints[i].m_SpawnPoint.rotation);

                    m_SpawnPoints[i].m_Instance = tankInstance;
                    m_SpawnPoints[i].m_PlayerNumber = i + 1;
                    m_SpawnPoints[i].ControlIndex = (i == 0) ? 1 : 2;
                    m_SpawnPoints[i].m_ComputerControlled = (i > 0);
                }

                foreach (var tank in m_SpawnPoints)
                {
                    if (tank.m_Instance == null) continue;
                    tank.Setup(this);
                }
                return;
            }

            if (!IsServer) return;

            var clients = NetworkManager.Singleton.ConnectedClientsList;
            m_PlayerCount = Mathf.Min(clients.Count, m_SpawnPoints.Length);

            for (int i = 0; i < m_PlayerCount; i++)
            {
                ulong clientId = clients[i].ClientId;
                // Dùng xe mà chính máy đó đã chọn; nếu thiếu (fallback) thì theo thứ tự slot.
                int choice = m_ClientTankChoice.TryGetValue(clientId, out var picked) ? picked : i;
                GameObject tankPrefab = GetTankPrefabByIndex(choice);
                Debug.Log($"[GameManager] Spawn tank cho client {clientId} bằng prefab '{tankPrefab.name}' (index {choice}).");

                GameObject tankInstance = Instantiate(tankPrefab, m_SpawnPoints[i].m_SpawnPoint.position, m_SpawnPoints[i].m_SpawnPoint.rotation);
                tankInstance.GetComponent<NetworkObject>().SpawnWithOwnership(clientId);

                m_SpawnPoints[i].m_Instance = tankInstance;
                m_SpawnPoints[i].m_PlayerNumber = i + 1;
                m_SpawnPoints[i].ControlIndex = -1;
                m_SpawnPoints[i].m_ComputerControlled = false;
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

            // Once execution has returned here, run the 'RoundEnding' coroutine, again don't return until it's finished.
            yield return StartCoroutine (RoundEnding());

            // This code is not run until 'RoundEnding' has finished.  At which point, check if a game winner has been found.
            if (m_GameWinner != null)
            {
                // If there is a game winner, return to the main menu so the player can pick again.
                // Loaded by name (not index 0) because index 0 is now the Start screen.
                SceneManager.LoadScene ("MainMenu");
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
            SetTitleText("ROUND " + m_RoundNumber);

            // Wait for the specified length of time until yielding control back to the game loop.
            yield return m_StartWait;
        }


        private IEnumerator RoundPlaying ()
        {
            // As soon as the round begins playing let the players control the tanks.
            EnableTankControl ();

            // Clear the text from the screen.
            SetTitleText("");
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                GameStart();
            }

            // While there is not one tank left...
            while (!OneTankLeft())
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


        // This is used to check if there is one or fewer tanks remaining and thus the round should end.
        private bool OneTankLeft()
        {
            // Start the count of tanks left at zero.
            int numTanksLeft = 0;

            // Go through all the tanks...
            for (int i = 0; i < m_PlayerCount; i++)
            {
                // ... and if they are active, increment the counter.
                if (m_SpawnPoints[i].m_Instance.activeSelf)
                    numTanksLeft++;
            }

            // If there are one or fewer tanks remaining return true, otherwise return false.
            return numTanksLeft <= 1;
        }
        
        
        // This function is to find out if there is a winner of the round.
        // This function is called with the assumption that 1 or fewer tanks are currently active.
        private TankManager GetRoundWinner()
        {
            // Go through all the tanks...
            for (int i = 0; i < m_PlayerCount; i++)
            {
                // ... and if one of them is active, it is the winner so return it.
                if (m_SpawnPoints[i].m_Instance.activeSelf)
                    return m_SpawnPoints[i];
            }

            // If none of the tanks are active it is a draw so return null.
            return null;
        }


        // This function is to find out if there is a winner of the game.
        private TankManager GetGameWinner()
        {
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
            // By default when a round ends there are no winners so the default end message is a draw.
            string message = "DRAW!";

            // If there is a winner then change the message to reflect that.
            if (m_RoundWinner != null)
                message = m_RoundWinner.m_ColoredPlayerText + " WINS THE ROUND!";

            // Add some line breaks after the initial message.
            message += "\n\n\n\n";

            // Go through all the tanks and add each of their scores to the message.
            for (int i = 0; i < m_PlayerCount; i++)
            {
                message += m_SpawnPoints[i].m_ColoredPlayerText + ": " + m_SpawnPoints[i].m_Wins + " WINS\n";
            }

            // If there is a game winner, change the entire message to reflect that.
            if (m_GameWinner != null)
                message = m_GameWinner.m_ColoredPlayerText + " WINS THE GAME!";

            return message;
        }


        // This function is used to turn all the tanks back on and reset their positions and properties.
        private void ResetAllTanks()
        {
            for (int i = 0; i < m_PlayerCount; i++)
            {
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
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace Tanks.Complete
{
    /// <summary>
    /// Manages the UGS Lobby lifecycle. A lobby is the "room": players find/join it by its short
    /// <c>LobbyCode</c>. The host stashes the Relay join code inside the lobby's data so that a client,
    /// after joining the lobby by code, can read it and connect to the same Relay allocation.
    ///
    /// So the player only ever shares ONE code (the lobby code); the relay code is transported inside.
    /// Requires Lobby to be enabled for the project's environment on the Unity Dashboard.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        public const string RelayJoinCodeKey = "RelayJoinCode";

        public static LobbyManager Instance { get; private set; }

        public Lobby CurrentLobby { get; private set; }
        public bool IsHost { get; private set; }

        private Coroutine m_HeartbeatRoutine;

        public static LobbyManager Ensure()
        {
            if (Instance == null)
            {
                var go = new GameObject("LobbyManager");
                Instance = go.AddComponent<LobbyManager>();
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

        /// <summary>
        /// Host side: create a public lobby carrying the Relay join code. Returns the lobby code that
        /// the host shares with friends.
        /// </summary>
        public async Task<string> CreateLobbyAsync(string lobbyName, int maxPlayers, string relayJoinCode)
        {
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { RelayJoinCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            IsHost = true;

            // Keep the lobby alive: an idle lobby is auto-removed after ~30s without a heartbeat.
            m_HeartbeatRoutine = StartCoroutine(HeartbeatLoop(15f));

            Debug.Log($"[Lobby] Created '{CurrentLobby.Name}' code={CurrentLobby.LobbyCode} id={CurrentLobby.Id}");
            return CurrentLobby.LobbyCode;
        }

        /// <summary>
        /// Client side: join a lobby by its code and return the Relay join code stored inside it.
        /// </summary>
        public async Task<string> JoinLobbyByCodeAsync(string lobbyCode)
        {
            CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            IsHost = false;

            string relayJoinCode = null;
            if (CurrentLobby.Data != null && CurrentLobby.Data.TryGetValue(RelayJoinCodeKey, out var dataObject))
            {
                relayJoinCode = dataObject.Value;
            }

            Debug.Log($"[Lobby] Joined '{CurrentLobby.Name}' code={CurrentLobby.LobbyCode} relay={relayJoinCode}");
            return relayJoinCode;
        }

        private IEnumerator HeartbeatLoop(float intervalSeconds)
        {
            var wait = new WaitForSeconds(intervalSeconds);
            while (CurrentLobby != null)
            {
                _ = SendHeartbeatAsync(CurrentLobby.Id);
                yield return wait;
            }
        }

        private static async Task SendHeartbeatAsync(string lobbyId)
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[Lobby] Heartbeat failed: {e.Message}");
            }
        }

        /// <summary>Leave/delete the current lobby and stop heartbeating.</summary>
        public async Task LeaveLobbyAsync()
        {
            if (m_HeartbeatRoutine != null)
            {
                StopCoroutine(m_HeartbeatRoutine);
                m_HeartbeatRoutine = null;
            }

            if (CurrentLobby == null)
                return;

            var lobbyId = CurrentLobby.Id;
            var wasHost = IsHost;
            CurrentLobby = null;

            try
            {
                if (wasHost)
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                else
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
            }
            catch (LobbyServiceException e)
            {
                Debug.LogWarning($"[Lobby] Leave failed: {e.Message}");
            }
        }
    }
}
